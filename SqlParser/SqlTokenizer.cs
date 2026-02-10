/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlParser
 * FILE:        SqlTokenizer.cs
 * PURPOSE:     Tokenizes SQL input, properly capturing inline comments.
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
                    var closeQuote = c;
                    int start = ++i;
                    while (i < input.Length && input[i] != closeQuote) i++;

                    // Bugfix: Safety check in case string is unclosed at end of line
                    if (i <= input.Length)
                    {
                        string val = input.Substring(start, i - start);
                        tokens.Enqueue(new Token(TokenType.StringLiteral, val));
                        i++; // Skip closing quote
                    }
                    continue;
                }

                // 3. Comments (FIXED: Capture instead of Skip)
                if (c == '-' && i + 1 < input.Length && input[i + 1] == '-')
                {
                    // Move past the "--"
                    i += 2;

                    // Capture everything remaining in the line
                    string commentText = "";
                    if (i < input.Length)
                    {
                        commentText = input.Substring(i).Trim();
                    }

                    if (!string.IsNullOrEmpty(commentText))
                    {
                        tokens.Enqueue(new Token(TokenType.Comment, commentText));
                    }

                    // We consumed the rest of the line, so we are done
                    break;
                }

                // 4. Identifiers / Keywords / Numbers
                int wordStart = i;
                // Read until whitespace or a special character
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