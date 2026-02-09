/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlGenerator
 * FILE:        ColumnDef.cs
 * PURPOSE:     Simple record to hold column definitions for SQL generation
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

namespace SqlGenerator
{
    /// <summary>
    /// Generic record to hold column definitions for SQL generation, including name, type, nullability, default value, and description.
    /// </summary>
    /// <seealso cref="System.IEquatable&lt;SqlGenerator.ColumnDef&gt;" />
    public record ColumnDef(string Name, string Type, string Nullable, string Default, string Description);
}
