#region Header

/*
 * JsonReader.cs
 *   Stream-like access to JSON text.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 */

#endregion


using System;
using System.Collections.Generic;
using System.IO;

namespace LitJson;

public enum JsonToken
{
    None,

    ObjectStart,
    PropertyName,
    ObjectEnd,

    ArrayStart,
    ArrayEnd,

    Int,
    Long,
    Double,

    String,

    Boolean,
    Null
}

public class JsonReader
{
    public void Close()
    {
        if (EndOfInput)
            return;

        EndOfInput = true;
        EndOfJson = true;

        if (reader_is_owned)
            reader.Close();

        reader = null;
    }

    public bool Read()
    {
        if (EndOfInput)
            return false;

        if (EndOfJson)
        {
            EndOfJson = false;
            automaton_stack.Clear();
            automaton_stack.Push((int)ParserToken.End);
            automaton_stack.Push((int)ParserToken.Text);
        }

        parser_in_string = false;
        parser_return = false;

        Token = JsonToken.None;
        Value = null;

        if (!read_started)
        {
            read_started = true;

            if (!ReadToken())
                return false;
        }


        int[] entry_symbols;

        while (true)
        {
            if (parser_return)
            {
                if (automaton_stack.Peek() == (int)ParserToken.End)
                    EndOfJson = true;

                return true;
            }

            current_symbol = automaton_stack.Pop();

            ProcessSymbol();

            if (current_symbol == current_input)
            {
                if (!ReadToken())
                {
                    if (automaton_stack.Peek() != (int)ParserToken.End)
                        throw new JsonException(
                            "Input doesn't evaluate to proper JSON text");

                    if (parser_return)
                        return true;

                    return false;
                }

                continue;
            }

            try
            {
                entry_symbols =
                    parse_table[current_symbol][current_input];
            }
            catch (KeyNotFoundException e)
            {
                throw new JsonException((ParserToken)current_input, e);
            }

            if (entry_symbols[0] == (int)ParserToken.Epsilon)
                continue;

            for (var i = entry_symbols.Length - 1; i >= 0; i--)
                automaton_stack.Push(entry_symbols[i]);
        }
    }

    #region Fields

    private static IDictionary<int, IDictionary<int, int[]>> parse_table;

    private readonly Stack<int> automaton_stack;
    private int current_input;
    private int current_symbol;
    private readonly Lexer lexer;
    private bool parser_in_string;
    private bool parser_return;
    private bool read_started;
    private TextReader reader;
    private readonly bool reader_is_owned;

    #endregion


    #region Public Properties

    public bool AllowComments
    {
        get => lexer.AllowComments;
        set => lexer.AllowComments = value;
    }

    public bool AllowSingleQuotedStrings
    {
        get => lexer.AllowSingleQuotedStrings;
        set => lexer.AllowSingleQuotedStrings = value;
    }

    public bool SkipNonMembers { get; set; }

    public bool EndOfInput { get; private set; }

    public bool EndOfJson { get; private set; }

    public JsonToken Token { get; private set; }

    public object Value { get; private set; }

    #endregion


    #region Constructors

    static JsonReader()
    {
        PopulateParseTable();
    }

    public JsonReader(string json_text) :
        this(new StringReader(json_text), true)
    {
    }

    public JsonReader(TextReader reader) :
        this(reader, false)
    {
    }

    private JsonReader(TextReader reader, bool owned)
    {
        if (reader == null)
            throw new ArgumentNullException("reader");

        parser_in_string = false;
        parser_return = false;

        read_started = false;
        automaton_stack = new Stack<int>();
        automaton_stack.Push((int)ParserToken.End);
        automaton_stack.Push((int)ParserToken.Text);

        lexer = new Lexer(reader);

        EndOfInput = false;
        EndOfJson = false;

        SkipNonMembers = true;

        this.reader = reader;
        reader_is_owned = owned;
    }

    #endregion


    #region Static Methods

    private static void PopulateParseTable()
    {
        // See section A.2. of the manual for details
        parse_table = new Dictionary<int, IDictionary<int, int[]>>();

        TableAddRow(ParserToken.Array);
        TableAddCol(ParserToken.Array, '[',
            '[',
            (int)ParserToken.ArrayPrime);

        TableAddRow(ParserToken.ArrayPrime);
        TableAddCol(ParserToken.ArrayPrime, '"',
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest,
            ']');
        TableAddCol(ParserToken.ArrayPrime, '[',
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest,
            ']');
        TableAddCol(ParserToken.ArrayPrime, ']',
            ']');
        TableAddCol(ParserToken.ArrayPrime, '{',
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest,
            ']');
        TableAddCol(ParserToken.ArrayPrime, (int)ParserToken.Number,
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest,
            ']');
        TableAddCol(ParserToken.ArrayPrime, (int)ParserToken.True,
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest,
            ']');
        TableAddCol(ParserToken.ArrayPrime, (int)ParserToken.False,
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest,
            ']');
        TableAddCol(ParserToken.ArrayPrime, (int)ParserToken.Null,
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest,
            ']');

        TableAddRow(ParserToken.Object);
        TableAddCol(ParserToken.Object, '{',
            '{',
            (int)ParserToken.ObjectPrime);

        TableAddRow(ParserToken.ObjectPrime);
        TableAddCol(ParserToken.ObjectPrime, '"',
            (int)ParserToken.Pair,
            (int)ParserToken.PairRest,
            '}');
        TableAddCol(ParserToken.ObjectPrime, '}',
            '}');

        TableAddRow(ParserToken.Pair);
        TableAddCol(ParserToken.Pair, '"',
            (int)ParserToken.String,
            ':',
            (int)ParserToken.Value);

        TableAddRow(ParserToken.PairRest);
        TableAddCol(ParserToken.PairRest, ',',
            ',',
            (int)ParserToken.Pair,
            (int)ParserToken.PairRest);
        TableAddCol(ParserToken.PairRest, '}',
            (int)ParserToken.Epsilon);

        TableAddRow(ParserToken.String);
        TableAddCol(ParserToken.String, '"',
            '"',
            (int)ParserToken.CharSeq,
            '"');

        TableAddRow(ParserToken.Text);
        TableAddCol(ParserToken.Text, '[',
            (int)ParserToken.Array);
        TableAddCol(ParserToken.Text, '{',
            (int)ParserToken.Object);

        TableAddRow(ParserToken.Value);
        TableAddCol(ParserToken.Value, '"',
            (int)ParserToken.String);
        TableAddCol(ParserToken.Value, '[',
            (int)ParserToken.Array);
        TableAddCol(ParserToken.Value, '{',
            (int)ParserToken.Object);
        TableAddCol(ParserToken.Value, (int)ParserToken.Number,
            (int)ParserToken.Number);
        TableAddCol(ParserToken.Value, (int)ParserToken.True,
            (int)ParserToken.True);
        TableAddCol(ParserToken.Value, (int)ParserToken.False,
            (int)ParserToken.False);
        TableAddCol(ParserToken.Value, (int)ParserToken.Null,
            (int)ParserToken.Null);

        TableAddRow(ParserToken.ValueRest);
        TableAddCol(ParserToken.ValueRest, ',',
            ',',
            (int)ParserToken.Value,
            (int)ParserToken.ValueRest);
        TableAddCol(ParserToken.ValueRest, ']',
            (int)ParserToken.Epsilon);
    }

    private static void TableAddCol(ParserToken row, int col,
        params int[] symbols)
    {
        parse_table[(int)row].Add(col, symbols);
    }

    private static void TableAddRow(ParserToken rule)
    {
        parse_table.Add((int)rule, new Dictionary<int, int[]>());
    }

    #endregion


    #region Private Methods

    private void ProcessNumber(string number)
    {
        if (number.IndexOf('.') != -1 ||
            number.IndexOf('e') != -1 ||
            number.IndexOf('E') != -1)
        {
            double n_double;
            if (double.TryParse(number, out n_double))
            {
                Token = JsonToken.Double;
                Value = n_double;

                return;
            }
        }

        int n_int32;
        if (int.TryParse(number, out n_int32))
        {
            Token = JsonToken.Int;
            Value = n_int32;

            return;
        }

        long n_int64;
        if (long.TryParse(number, out n_int64))
        {
            Token = JsonToken.Long;
            Value = n_int64;

            return;
        }

        ulong n_uint64;
        if (ulong.TryParse(number, out n_uint64))
        {
            Token = JsonToken.Long;
            Value = n_uint64;

            return;
        }

        // Shouldn't happen, but just in case, return something
        Token = JsonToken.Int;
        Value = 0;
    }

    private void ProcessSymbol()
    {
        if (current_symbol == '[')
        {
            Token = JsonToken.ArrayStart;
            parser_return = true;
        }
        else if (current_symbol == ']')
        {
            Token = JsonToken.ArrayEnd;
            parser_return = true;
        }
        else if (current_symbol == '{')
        {
            Token = JsonToken.ObjectStart;
            parser_return = true;
        }
        else if (current_symbol == '}')
        {
            Token = JsonToken.ObjectEnd;
            parser_return = true;
        }
        else if (current_symbol == '"')
        {
            if (parser_in_string)
            {
                parser_in_string = false;

                parser_return = true;
            }
            else
            {
                if (Token == JsonToken.None)
                    Token = JsonToken.String;

                parser_in_string = true;
            }
        }
        else if (current_symbol == (int)ParserToken.CharSeq)
        {
            Value = lexer.StringValue;
        }
        else if (current_symbol == (int)ParserToken.False)
        {
            Token = JsonToken.Boolean;
            Value = false;
            parser_return = true;
        }
        else if (current_symbol == (int)ParserToken.Null)
        {
            Token = JsonToken.Null;
            parser_return = true;
        }
        else if (current_symbol == (int)ParserToken.Number)
        {
            ProcessNumber(lexer.StringValue);

            parser_return = true;
        }
        else if (current_symbol == (int)ParserToken.Pair)
        {
            Token = JsonToken.PropertyName;
        }
        else if (current_symbol == (int)ParserToken.True)
        {
            Token = JsonToken.Boolean;
            Value = true;
            parser_return = true;
        }
    }

    private bool ReadToken()
    {
        if (EndOfInput)
            return false;

        lexer.NextToken();

        if (lexer.EndOfInput)
        {
            Close();

            return false;
        }

        current_input = lexer.Token;

        return true;
    }

    #endregion
}