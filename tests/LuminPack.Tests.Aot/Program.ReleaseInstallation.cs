using LuminPack;
using LuminPack.Attribute;

internal static partial class Program
{
    /// <summary>Verifies local NuGet installation through generated binary/JSON APIs and checked reads.</summary>
    private static void CheckPackageInstallation()
    {
        var source = new PackageInstallationRecord { Id = 17, Flag = true, Text = "package", Values = [1, 2, 3] };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        PackageInstallationRecord fresh = null!;
        Check(LuminPackSerializer.Deserialize(bytes, ref fresh) == bytes.Length, "The installed binary reader consumed an unexpected length.");
        Check(fresh.Id == source.Id && fresh.Flag && fresh.Text == source.Text && fresh.Values.SequenceEqual(source.Values), "The installed binary formatter changed values.");
        var reused = new PackageInstallationRecord { Id = -1, Text = "old", Values = new int[3] };
        var identity = reused;
        var array = reused.Values;
        LuminPackSerializer.Deserialize(bytes, ref reused);
        Check(ReferenceEquals(identity, reused) && ReferenceEquals(array, reused.Values) && reused.Id == source.Id, "The installed binary formatter lost reuse.");
        string json = LuminPackSerializer.SerializeJson(source);
        PackageInstallationRecord fromJson = null!;
        LuminPackSerializer.DeserializeJson(json, ref fromJson);
        Check(fromJson.Id == source.Id && fromJson.Flag && fromJson.Text == source.Text && fromJson.Values.SequenceEqual(source.Values), "The installed JSON formatter changed values.");
        bool rejected = false;
        try
        {
            PackageInstallationRecord truncated = null!;
            LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref truncated);
        }
        catch (Exception exception) when (exception is InvalidDataException ||
            exception.GetType().FullName == "LuminPack.Code.LuminPackExceptionHelper+LuminPackException") { rejected = true; }
        Check(rejected, "The installed checked reader accepted truncated input.");
        Console.WriteLine("Passed installed binary/JSON formatters, reuse and checked-input boundary.");
    }
}

/// <summary>A small package-consumer contract independent of the retained wire fixtures.</summary>
[LuminPackable]
[LuminPackOption(UntrustedData = true, MaxInputBytes = 1024, MaxCollectionLength = 4, MaxStringLength = 16, MaxDepth = 4)]
public partial class PackageInstallationRecord
{
    public int Id;
    public bool Flag;
    public string Text = "";
    public int[] Values = [];
}
