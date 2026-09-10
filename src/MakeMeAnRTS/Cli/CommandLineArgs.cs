namespace MakeMeAnRTS.Cli;

/// <summary>
/// A deliberately small parser for <c>--name value</c> and <c>--flag</c> arguments.
/// </summary>
/// <remarks>
/// Reading an option that was never declared throws. Debug commands accumulate options fast, and a
/// silently ignored typo in a flag wastes far more time than a loud failure.
/// </remarks>
public sealed class CommandLineArgs
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The first non-option argument, if any: the command name.</summary>
    public string? Command { get; }

    public CommandLineArgs(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var index = 0;

        if (args.Count > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Command = args[0];
            index = 1;
        }

        while (index < args.Count)
        {
            var argument = args[index];

            if (!argument.StartsWith("--", StringComparison.Ordinal))
                throw new CommandLineException($"Unexpected argument '{argument}'. Options must start with '--'.");

            var name = argument[2..];
            var hasValue = index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal);

            _options[name] = hasValue ? args[index + 1] : null;
            index += hasValue ? 2 : 1;
        }
    }

    public bool HasFlag(string name) => _options.ContainsKey(name);

    public string String(string name, string fallback) => _options.TryGetValue(name, out var value) && value is not null ? value : fallback;

    public string RequiredString(string name) =>
        _options.TryGetValue(name, out var value) && value is not null
            ? value
            : throw new CommandLineException($"Option '--{name}' is required and needs a value.");

    public int Int(string name, int fallback)
    {
        if (!_options.TryGetValue(name, out var value) || value is null)
            return fallback;

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new CommandLineException($"Option '--{name}' expects a whole number, got '{value}'.");
    }

    public float Float(string name, float fallback)
    {
        if (!_options.TryGetValue(name, out var value) || value is null)
            return fallback;

        return float.TryParse(value, out var parsed)
            ? parsed
            : throw new CommandLineException($"Option '--{name}' expects a number, got '{value}'.");
    }

    /// <summary>Parses an enum option by name, case-insensitively, listing the valid values on failure.</summary>
    public TEnum? Enum<TEnum>(string name) where TEnum : struct, Enum
    {
        if (!_options.TryGetValue(name, out var value) || value is null)
            return null;

        if (System.Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
            return parsed;

        throw new CommandLineException($"Option '--{name}' expects one of: {string.Join(", ", System.Enum.GetNames<TEnum>())}. Got '{value}'.");
    }
}

public sealed class CommandLineException(string message) : Exception(message);
