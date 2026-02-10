/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlParser
 * FILE:        SqlTokenizer.cs
 * PURPOSE:     Your file purpose here
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

namespace SqlParser
{
    public class SqlTokenizer
    {
        public Queue<Token> Tokenize(string input)
        {
            var tokens = new Queue<Token>();
            var i = 0;
            while (i < input.Length)
            {
                char c = input[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                // 1. Parentheses & Commas
                if (c == '(') { tokens.Enqueue(new Token(TokenType.OpenParen, "(")); i++; continue; }
                if (c == ')') { tokens.Enqueue(new Token(TokenType.CloseParen, ")")); i++; continue; }
                if (c == ',') { tokens.Enqueue(new Token(TokenType.Comma, ",")); i++; continue; }

                // 2. String Literals (Description)
                if (c == '"' || c == '\'')
                {
                    var closeQuote = c; // Handle " or '
                    int start = ++i;
                    while (i < input.Length && input[i] != closeQuote) i++;
                    string val = input.Substring(start, i - start);
                    tokens.Enqueue(new Token(TokenType.StringLiteral, val));
                    i++; // Skip closing quote
                    continue;
                }

                // 3. Comments (Skip rest of line)
                if (c == '-' && i + 1 < input.Length && input[i + 1] == '-')
                    break;

                // 4. Identifiers / Keywords / Numbers
                // We treat anything that isn't a symbol as a "word" for now
                int wordStart = i;
                while (i < input.Length && !char.IsWhiteSpace(input[i]) && "(),".IndexOf(input[i]) == -1)
                    i++;

                string word = input.Substring(wordStart, i - wordStart);
                tokens.Enqueue(new Token(TokenType.Identifier, word));
            }

            tokens.Enqueue(new Token(TokenType.EndOfLine, ""));
            return tokens;
        }
    }
}
