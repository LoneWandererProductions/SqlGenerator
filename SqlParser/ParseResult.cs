/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlParser
 * FILE:        ParseResult.cs
 * PURPOSE:     Your file purpose here
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlParser
{
    public class ParseResult
    {
        public List<ColumnDef> Columns { get; } = new();
        public List<string> DebugTrace { get; } = new();
        public bool HasErrors => DebugTrace.Any(x => x.StartsWith("[ERROR]"));

        public void Log(string stage, string message)
        {
            DebugTrace.Add($"[{stage.PadRight(6)}] {message}");
        }
    }
}
