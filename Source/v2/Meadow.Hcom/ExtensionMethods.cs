namespace Meadow;

/// <summary>
/// ExtensionMethods class
/// </summary>
public static class ExtensionMethods
{
    /// <summary>
    /// Contains static extention method to check if the pattern exists within the source
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <param name="source"></param>
    /// <param name="pattern"></param>
    /// <returns>true if the pattern exists</returns>
    // TODO: move this into the `CircularBuffer` class? or is it broadly applicable?
    public static bool Contains<TSource>(this IEnumerable<TSource> source, TSource[] pattern)
    {
        return (source.FirstIndexOf(pattern) != -1);
    }

    /// <summary>
    /// FirstIndexOf static extention method for an IEnumerable
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <param name="source"></param>
    /// <param name="pattern"></param>
    /// <returns>the index position of the found pattern</returns>
    // TODO: move this into the `CircularBuffer` class? or is it broadly applicable?
    public static int FirstIndexOf<TSource>(this IEnumerable<TSource> source, TSource[] pattern)
    {
        if (pattern == null) throw new ArgumentNullException();

        int patternLength = pattern.Length;
        int totalLength = source.Count();
        TSource firstMatch = pattern[0];

        if (firstMatch == null) return -1;

        for (int i = 0; i < totalLength; i++)
        {
            // is this the right equality?
            if ((firstMatch.Equals(source.ElementAt(i))) // begin match?
                 &&
                 (totalLength - i >= patternLength) // can match exist?
               )
            {
                TSource[] matchTest = new TSource[patternLength];
                // copy the potential match into the matchTest array.
                // can't use .Skip() and .Take() because it will actually
                // enumerate over stuff and can have side effects
                for (int x = 0; x < patternLength; x++)
                {
                    matchTest[x] = source.ElementAt(i + x);
                }
                // if the pattern pulled from source matches our search pattern
                // then the pattern exists.
                if (matchTest.SequenceEqual(pattern))
                {
                    return i;
                }
            }
        }
        // if we go here, doesn't exist
        return -1;
    }
}