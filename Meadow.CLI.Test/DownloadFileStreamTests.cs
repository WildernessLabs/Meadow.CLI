using System.IO;
using Meadow.CLI.Core.Common;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Meadow.CLI.Test
{
    [TestFixture]
    public class DownloadFileStreamTests
    {
        private const int _bufferSize = 1024;

        [Test]
        public void CanRead_Should_Return_True()
        {
            // Arrange
            var stream = new MemoryStream();
            var loggerMock = new Mock<ILogger>();
            var downloadStream = new DownloadFileStream(stream, loggerMock.Object);

            // Act
            var canRead = downloadStream.CanRead;

            // Assert
            Assert.IsTrue(canRead);
        }

        [Test]
        public void CanSeek_Should_Return_False()
        {
            // Arrange
            var stream = new MemoryStream();
            var loggerMock = new Mock<ILogger>();
            var downloadStream = new DownloadFileStream(stream, loggerMock.Object);

            // Act
            var canSeek = downloadStream.CanSeek;

            // Assert
            Assert.IsFalse(canSeek);
        }

        [Test]
        public void CanWrite_Should_Return_False()
        {
            // Arrange
            var stream = new MemoryStream();
            var loggerMock = new Mock<ILogger>();
            var downloadStream = new DownloadFileStream(stream, loggerMock.Object);

            // Act
            var canWrite = downloadStream.CanWrite;

            // Assert
            Assert.IsFalse(canWrite);
        }

        [Test]
        public void Length_Should_Return_Stream_Length()
        {
            // Arrange
            var buffer = new byte[_bufferSize];
            var expectedLength = _bufferSize * 2; // Total size: 2048 bytes
            var stream = new MemoryStream(buffer);
            var loggerMock = new Mock<ILogger>();
            var downloadStream = new DownloadFileStream(stream, loggerMock.Object);

            // Act
            var actualLength = downloadStream.Length;

            // Assert
            Assert.AreEqual(expectedLength, actualLength);
        }

        [Test]
        public void Read_Should_Update_Position_And_Log_Downloaded_Data()
        {
            // Arrange
            var buffer = new byte[_bufferSize];
            var stream = new MemoryStream(buffer);
            var loggerMock = new Mock<ILogger>();
            var downloadStream = new DownloadFileStream(stream, loggerMock.Object);
            var readBuffer = new byte[_bufferSize];

            // Act
            var bytesRead = downloadStream.Read(readBuffer, 0, _bufferSize);

            // Assert
            Assert.AreEqual(_bufferSize, bytesRead);
            Assert.AreEqual(_bufferSize, downloadStream.Position);
            loggerMock.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<object>()), Times.Once());
        }
    }
}