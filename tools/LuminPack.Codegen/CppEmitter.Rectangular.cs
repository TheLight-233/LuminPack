internal sealed partial class CppEmitter
{
    /// <summary>Wraps the resolved contiguous element record with the CLR rectangular shape.</summary>
    /// <param name="rank">The number of dimensions.</param>
    /// <param name="sequence">The binary and JSON sequence selected for the element.</param>
    /// <returns>The rectangular binary and JSON codecs.</returns>
    private static Binding BindRectangular(int rank, Binding sequence)
    {
        string binary = Apply("luminpack::rectangular_array_codec", new[] { Integer(rank), sequence.Binary });
        string json = Apply("luminpack::json_rectangular_array_codec", new[] { Integer(rank), sequence.Json });
        return new Binding(binary + "::value_type", binary, json);
    }

    /// <summary>Extracts the outer rectangular rank from a closed CLR array spelling.</summary>
    /// <param name="type">The normalized CLR name, including any nullable reference suffix.</param>
    /// <param name="element">Receives the nested element name when the outer array is rectangular.</param>
    /// <returns>The rectangular rank, or zero for a different kind of declaration.</returns>
    private static int RectangularRank(string type, out string element)
    {
        element = string.Empty;
        int end = type.EndsWith('?') ? type.Length - 1 : type.Length;
        if (end < 4 || type[end - 1] != ']')
            return 0;

        int start = type.LastIndexOf('[', end - 1);
        if (start < 1 || end - start < 3)
            return 0;
        for (int index = start + 1; index < end - 1; index++)
            if (type[index] != ',')
                return 0;
        element = type[..start];
        return end - start - 1;
    }
}
