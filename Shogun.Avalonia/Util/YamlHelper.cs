using System;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Shogun.Avalonia.Util;

/// <summary>
/// YamlDotNet を用いた YAML シリアライズ／デシリアライズのヘルパー。
/// </summary>
public static class YamlHelper
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    /// <summary>
    /// バイト列を YAML としてデシリアライズする。
    /// </summary>
    /// <typeparam name="T">対象型。</typeparam>
    /// <param name="bytes">UTF-8 の YAML バイト列。</param>
    /// <returns>デシリアライズ結果。失敗時は null。</returns>
    public static T? Deserialize<T>(byte[] bytes) where T : class
    {
        if (bytes == null || bytes.Length == 0)
            return null;
        var text = Encoding.UTF8.GetString(bytes);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        using var reader = new StringReader(text);
        try
        {
            return Deserializer.Deserialize<T>(reader);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// オブジェクトを YAML にシリアライズし、UTF-8 バイト列で返す。
    /// </summary>
    /// <param name="value">シリアライズするオブジェクト。</param>
    /// <returns>UTF-8 の YAML バイト列。</returns>
    public static byte[] SerializeToBytes(object? value)
    {
        if (value == null)
            return Array.Empty<byte>();
        var yaml = Serializer.Serialize(value);
        return Encoding.UTF8.GetBytes(yaml);
    }
}
