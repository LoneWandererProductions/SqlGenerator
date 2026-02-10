/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlParser
 * FILE:        ColumnDef.cs
 * PURPOSE:     Simple record to hold column definitions for SQL generation
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

namespace SqlParser
{
    /// <summary>
    /// Generic record to hold column definitions for SQL generation, including name, type, nullability, default value, and description.
    /// </summary>
    /// <seealso cref="IEquatable&lt;ColumnDef&gt;" />
    public record ColumnDef(string Name, string Type, string Nullable, string Default, string Description);
}
