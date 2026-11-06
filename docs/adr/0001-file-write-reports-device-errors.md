# ADR 0001: File writes report device-side failures

## Status

Proposed (2026-11-05). Fixes WildernessLabs/Meadow_Issues#890.

## Context

`meadow file write` printed 100% and exited 0 when the device's file system was full. The file was not written and the user was not told.

What the device does (Meadow.OS `apps/examples/hcom/file/hcom_file_dnld_stm32f7.c` and `comms/hcom_host_route.c`):

- If a data packet cannot be written, it sends `HCOM_HOST_REQUEST_TEXT_ERROR` ("Write of '<file>', seq N failed") once and drops the rest of the transfer.
- On `HCOM_MDOW_REQUEST_END_FILE_TRANSFER` it reads the file back, checks the CRC and size, and sends `TEXT_INFORMATION` ("Download of '<file>' succeeded") or `TEXT_ERROR` (checksum or size mismatch). It then sends `TEXT_CONCLUDED`, even on error. The route has behaved this way since 2020.
- A short write (fewer bytes written than requested, which is what a full file system can produce) is logged on the device but not reported. The end-of-transfer CRC check reports it as a checksum mismatch.

What the host did:

- `SerialConnection.WriteFile` sent the end message and returned `true` without waiting for the device's result. Errors arrived later as a `DeviceMessageReceived` event that `FileWriteCommand` does not subscribe to, and as `_lastError`, which is never cleared (Meadow_Issues#888).
- `SocketConnection.WriteFile` waited for `TEXT_CONCLUDED`, but `WaitForConcluded` returns early on any `_lastError`, and the result was ignored either way.
- `FileWriteCommand` ignored the `bool` returned by `WriteFile` and caught every exception, logging it, so the exit code was always 0.

## Decision

1. For the plain file transfer (`START_FILE_TRANSFER` / `END_FILE_TRANSFER`), `WriteFile` on both connections:
   - records any `TEXT_ERROR` the device sends during the transfer, and stops sending data once one arrives;
   - after the end message, waits for `TEXT_CONCLUDED` (20 ms polling, timeout `max(CommandTimeoutSeconds, 60 + bytes / 1000)` seconds on serial, matching the existing socket scaling);
   - throws `DeviceFileWriteException` (an `IOException`) carrying the device's message if one was recorded;
   - clears `_lastError` when it holds the error this call consumed, so the error does not leak into the next command.
2. The watchdog retry path (`HCOM_HOST_REQUEST_DNLD_FAIL_RESEND`) still returns `false`, so `AppManager`'s existing retry is unchanged.
3. `FileWriteCommand` prints each failure, continues with the remaining files, and exits with `GeneralError` (1) if any file failed. A mismatched `--files`/`--targetFiles` count exits with `InvalidParameter` (6) instead of 0.
4. Runtime (`MONO_UPDATE_RUNTIME`) and coprocessor (`ESP_FILE_TRANSFER`) writes are unchanged. They have their own end handlers and waits.
5. A failure to send a data packet propagates instead of breaking out of the loop. Before, the loop broke, the end message was still sent, and the write returned `true`.
6. `SerialConnection` ignores a `DNLD_FAIL_RESEND` that arrives before data transfer starts. A stale watchdog message from an earlier transfer used to make the next write retry for no reason. `SocketConnection` already had this guard (3e85cf6).

## Alternatives

- **Return `false` instead of throwing.** Rejected: `AppManager.DeployApplication` retries on `false` with no limit (`goto send_file`), so a full file system would loop forever during `meadow app deploy` and IDE deploys.
- **Treat checksum and size mismatches as retryable (`false`) and only a write failure as fatal.** This means telling the two apart by parsing the message text, and the retry is still unbounded. It could be revisited together with a bounded retry in `AppManager`.
- **Fix only `FileWriteCommand`.** Not enough: the serial connection never learns about the failure, so there is no result for the command to check.
- **Check free space before writing.** HCOM has no free-space request, and a check before the write would race with other writers. The device's own result is authoritative.

## Consequences

- A lost connection during a write now surfaces as an exception (and exit 1 from `file write`) on the serial transport. On the socket transport, `EncodeAndSendPacket` waits for a reconnect with no limit, so a lost connection hangs. That was already true before this change and is tracked separately.
- `meadow file write` exits non-zero and prints the device's error when a write fails. Scripts that relied on exit 0 after a partial failure will now see 1.
- `meadow app deploy`, the VS Code extension and the Visual Studio extension (both call `AppManager.DeployApplication` with no catch) now fail the deploy with the device's message instead of starting an app with missing or corrupt files. Related: Meadow_Issues#786.
- Firmware update steps that call `WriteFile` (`FirmwareUpdater`) and `MeadowSettingsManager` now surface write failures that were silent before.
- Serial writes wait for the device's `TEXT_CONCLUDED` per file, which includes the device's CRC readback. Not measured on hardware. Waiting also consumes the `TEXT_CONCLUDED` that previously arrived late and could satisfy a later request's wait.
- Any `TEXT_ERROR` the device sends during a transfer fails that transfer, including one unrelated to the file. `file write` and the CLI's deploy (`AppTools`) disable the runtime first. Whether the IDE extensions do was not checked.
- The message says what the device says. "Write of '<file>', seq N failed" does not name the cause. A firmware change is needed for "file system full" (see below).

## Implementation Status

- Done: `SerialConnection`, `SocketConnection`, `FileWriteCommand`, `DeviceFileWriteException`, on `fix/890-file-write-verdict` (based on `meadow-3.0`).
- Verified on the socket path against a fake HCOM device (disk full mid-transfer, checksum mismatch at end, success): exit 1 with the device message for both failures, exit 0 for success. The unmodified CLI exits 0 in all three. A connection dropped mid-transfer hangs both the unmodified and the changed CLI (see Consequences).
- Not verified: the serial path on hardware. Needs a run on an F7 with a full file system.
- Firmware follow-up (WildernessLabs/Meadow, not this repo): `hcom_file_write_to_active_file` returns `-1` instead of `-errno` on a failed `write()`, and treats a short write as success. Returning `-ENOSPC` for both and including the reason in the "Write of ..." message would let the CLI show "file system full" as the issue asks.
