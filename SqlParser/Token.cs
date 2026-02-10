/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlParser
 * FILE:        Token.cs
 * PURPOSE:     Your file purpose here
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

namespace SqlParser
{
    public record Token(TokenType Type, string Value);
}
