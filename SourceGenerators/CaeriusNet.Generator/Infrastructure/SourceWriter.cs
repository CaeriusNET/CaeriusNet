namespace CaeriusNet.Generator.Infrastructure;

[DebuggerDisplay("{ToString(),nq}")]
public sealed class SourceWriter
{
	private readonly StringBuilder _builder = new();

	private int Indentation { get; set; }

	public SourceWriter Append(char value)
	{
		_builder.Append(value);
		return this;
	}

	public SourceWriter Append(string value)
	{
		_builder.Append(value);
		return this;
	}

	public SourceWriter Append([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string value, params object[] args)
	{
		_builder.AppendFormat(value, args);
		return this;
	}

	public SourceWriter AppendIndented(char value)
	{
		_builder
			.Append(new string('\t', Indentation))
			.Append(value);
		return this;
	}

	public SourceWriter AppendIndented(string value)
	{
		_builder
			.Append(new string('\t', Indentation))
			.Append(value);
		return this;
	}

	public SourceWriter AppendIndented([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string value,
		params object[] args)
	{
		_builder
			.Append(new string('\t', Indentation))
			.AppendFormat(value, args);
		return this;
	}

	public SourceWriter AppendIndentedLine(char value)
	{
		_builder
			.Append(new string('\t', Indentation))
			.Append(value)
			.AppendLine();
		return this;
	}

	public SourceWriter AppendIndentedLine(string value)
	{
		_builder
			.Append(new string('\t', Indentation))
			.AppendLine(value);
		return this;
	}

	public SourceWriter AppendIndentedLine([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string value,
		params object[] args)
	{
		_builder
			.Append(new string('\t', Indentation))
			.AppendFormat(value, args)
			.AppendLine();
		return this;
	}

	public SourceWriter AppendLine()
	{
		_builder.AppendLine();
		return this;
	}

	public SourceWriter AppendLine(char value)
	{
		_builder
			.Append(value)
			.AppendLine();
		return this;
	}

	public SourceWriter AppendLine(string value)
	{
		_builder.AppendLine(value);
		return this;
	}

	public SourceWriter AppendLine([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string value,
		params object[] args)
	{
		_builder
			.AppendFormat(value, args)
			.AppendLine();
		return this;
	}

	public SourceWriter Clear()
	{
		_builder.Clear();
		Indentation = 0;
		return this;
	}

	public IDisposable CreateScope()
	{
		_builder
			.Append(new string('\t', Indentation))
			.AppendLine("{");

		Indentation++;

		return new DisposableAction(() =>
		{
			if (Indentation > 0)
				Indentation--;

			_builder
				.Append(new string('\t', Indentation))
				.AppendLine("}");
		});
	}

	public SourceWriter Indent()
	{
		Indentation++;
		return this;
	}

	public SourceText ToSourceText()
	{
		return SourceText.From(ToString(), Encoding.UTF8);
	}

	public override string ToString()
	{
		return _builder.ToString();
	}

	public SourceWriter Unindent()
	{
		if (Indentation > 0)
			Indentation--;
		return this;
	}
}