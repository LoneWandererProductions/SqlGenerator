/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlParser
 * FILE:        Executor.cs
 * PURPOSE:     Parses simplified SQL lines and generates structured column definitions.
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

using System.Text;

namespace SqlParser
{
    public class Executor
    {
        public ParseResult ParseAndGenerate(string input, string tableName)
        {
            var result = new ParseResult();

            // 1. TRACE INPUT
            result.Log("INPUT", $"Raw length: {input?.Length ?? 0} chars");
            var lines = (input ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 2. PARSE LOOP (Runs once!)
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                // Note: We let the Tokenizer handle comments starting with '--' 
                // via the TokenType.Comment, so we don't skip them here unless the line is ONLY a comment.
                if (trimmed.StartsWith("--"))
                {
                    result.Log("SKIP", $"Ignored comment line: '{trimmed}'");
                    continue;
                }

                try
                {
                    // ParseLine now returns the column, but logic errors throw exceptions 
                    // or return null which we handle here.
                    var col = ParseLine(trimmed);

                    if (col != null)
                    {
                        result.Columns.Add(col);
                        result.Log("PARSER", $"Column [{col.Name}] {col.Type} {(col.Nullable == "NOT NULL" ? "NOT NULL" : "")}");

                        if (!string.IsNullOrEmpty(col.Description))
                            result.Log("DOCS", $"Captured: '{col.Description}'");
                    }
                    else
                    {
                        result.Log("ERROR", $"ParseLine returned null for: '{trimmed}'");
                    }
                }
                catch (Exception ex)
                {
                    result.Log("ERROR", $"Exception parsing '{trimmed}': {ex.Message}");
                }
            }

            // 3. GENERATION TRACE
            result.Log("GEN", $"Ready to generate SQL for [dbo].[{tableName}]");

            return result;
        }

        public ColumnDef ParseLine(string line)
        {
            var tokens = new SqlTokenizer().Tokenize(line);
            if (tokens.Count == 0 || tokens.Peek().Type == TokenType.EndOfLine) return null;

            // 1. Expect Name & Normalize Brackets
            var nameToken = tokens.Dequeue();
            // Ensure name always has brackets [Name]
            string name = $"[{nameToken.Value.Trim('[', ']')}]";

            // 2. Expect Type
            if (tokens.Count == 0) return new ColumnDef(name, "UNKNOWN", "NULL", null, ""); // Safety

            var typeToken = tokens.Dequeue();
            string type = typeToken.Value;

            // Handle Type Parameters: e.g., DECIMAL ( 18 , 2 )
            if (tokens.Count > 0 && tokens.Peek().Type == TokenType.OpenParen)
            {
                type += tokens.Dequeue().Value; // (
                while (tokens.Count > 0 && tokens.Peek().Type != TokenType.CloseParen)
                {
                    type += tokens.Dequeue().Value;
                }
                if (tokens.Count > 0) type += tokens.Dequeue().Value; // )
            }

            // 3. Loop through the rest
            string nullable = "NULL";
            string def = null;
            string desc = "";

            while (tokens.Count > 0)
            {
                var t = tokens.Dequeue();
                if (t.Type == TokenType.EndOfLine) break;

                string upperVal = t.Value.ToUpperInvariant();

                // FIX: Check for Comment Token (from -- syntax)
                if (t.Type == TokenType.Comment)
                {
                    desc = t.Value;
                }
                else if (upperVal == "NOT" && tokens.Count > 0 && tokens.Peek().Value.ToUpperInvariant() == "NULL")
                {
                    nullable = "NOT NULL";
                    tokens.Dequeue(); // Consume 'NULL'
                }
                else if (upperVal == "NULL")
                {
                    nullable = "NULL";
                }
                else if (upperVal.StartsWith("DEFAULT"))
                {
                    // Handle both DEFAULT(0) and DEFAULT 0
                    if (tokens.Count > 0 && tokens.Peek().Type == TokenType.OpenParen)
                    {
                        def = "DEFAULT" + ReconstructGroup(tokens);
                    }
                    else if (tokens.Count > 0)
                    {
                        // Handle space-separated default (DEFAULT 0)
                        def = "DEFAULT " + tokens.Dequeue().Value;
                    }
                }
                else if (t.Type == TokenType.StringLiteral)
                {
                    desc = t.Value;
                }
                else
                {
                    // Capture implicit descriptions (words without quotes)
                    // Only append if we don't have a comment/quote description yet, or append to it.
                    desc += (desc.Length > 0 ? " " : "") + t.Value;
                }
            }

            return new ColumnDef(name, type, nullable, def, desc);
        }

        private string ReconstructGroup(Queue<Token> tokens)
        {
            if (tokens.Count == 0 || tokens.Peek().Type != TokenType.OpenParen) return string.Empty;

            var sb = new StringBuilder();
            int balance = 0;

            do
            {
                if (tokens.Count == 0) throw new Exception("Syntax Error: Unbalanced Parentheses.");

                var token = tokens.Dequeue();
                if (token.Type == TokenType.OpenParen) balance++;
                else if (token.Type == TokenType.CloseParen) balance--;

                sb.Append(token.Value);
            } while (balance > 0);

            return sb.ToString();
        }
    }
}