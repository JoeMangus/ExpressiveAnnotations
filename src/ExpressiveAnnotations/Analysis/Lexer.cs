﻿/* https://github.com/JaroslawWaliszko/ExpressiveAnnotations
 * Copyright (c) 2014 Jaroslaw Waliszko
 * Licensed MIT: http://opensource.org/licenses/MIT */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExpressiveAnnotations.Analysis
{
    /// <summary>
    /// Performs lexical analysis of provided logical expression.
    /// </summary>
    public sealed class Lexer
    {
        private Token Token { get; set; }
        private ParseState CurrentContext { get; set; }
        private IDictionary<TokenType, Regex> RegexMap { get; set; }        

        /// <summary>
        /// Initializes a new instance of the <see cref="Lexer" /> class.
        /// </summary>
        public Lexer()
        {            
            // special characters (should be escaped if needed): .$^{[(|)*+?\
            var patterns = new Dictionary<TokenType, string>
            {
                {TokenType.AND, @"&&"},
                {TokenType.OR, @"\|\|"},
                {TokenType.LEFT_BRACKET, @"\("},
                {TokenType.RIGHT_BRACKET, @"\)"},
                {TokenType.GE, @">="},
                {TokenType.LE, @"<="},
                {TokenType.GT, @">"},
                {TokenType.LT, @"<"},
                {TokenType.EQ, @"=="},
                {TokenType.NEQ, @"!="},
                {TokenType.NOT, @"!"},
                {TokenType.NULL, @"null"},
                {TokenType.COMMA, @","},
                {TokenType.INC, @"\+{2}"}, // Despite the fact our language does not support ++ and -- prefix/postfix operations yet, these unary tokens are explicitly designated as illegal. We're detecting them to prevent unification which is done for consecutive plus or minus operators (e.g. + + - - +-+- => +).
                {TokenType.DEC, @"\-{2}"}, // Unification of such unary operators breaks compatibility - such mixed 1++ + 2 operations are illegal in both C# and JavaScript (since we control C# side there is not pain here, but JavaScript would fail since we're sending raw expressions to client-side).
                {TokenType.ADD, @"\+"},
                {TokenType.SUB, @"-"},
                {TokenType.MUL, @"\*"},
                {TokenType.DIV, @"/"},
                {TokenType.FLOAT, @"[0-9]*\.[0-9]+(?:[eE][\+-]?[0-9]+)?"}, // 1.0, 0.3e-2
                {TokenType.INT, @"[0-9]+"},
                {TokenType.BOOL, @"(?:true|false)"},
                {TokenType.STRING, @"(['])(?:\\\1|.)*?\1"}, // '1234', 'John\'s cat'
                {TokenType.FUNC, @"[a-zA-Z_]+(?:(?:\.[a-zA-Z_])?[a-zA-Z0-9_]*)*"} // field, field.value, func(...)
            };

            RegexMap = patterns.ToDictionary(
                kvp => kvp.Key,
                kvp => new Regex(string.Format("^{0}", kvp.Value), RegexOptions.Compiled));
        }

        /// <summary>
        /// Analyzes the specified logical expression and extracts the array of tokens.
        /// </summary>
        /// <param name="expression">The logical expression.</param>
        /// <returns>
        /// Array of extracted tokens.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">expression;Expression not provided.</exception>
        public IEnumerable<Token> Analyze(string expression)
        {
            if (expression == null)
                throw new ArgumentNullException("expression", "Expression not provided.");
                                    
            CurrentContext = new ParseState(expression, line: 1, column: 1);

            var tokens = new List<Token>();
            while (Next())
                tokens.Add(Token);

            // once we've reached the end of the string, EOF token is returned - thus, parser's lookahead does not have to worry about running out of tokens
            Token = new Token(TokenType.EOF, string.Empty, new ParseState(CurrentContext));
            tokens.Add(Token);
            
            return tokens;
        }

        private bool Next()
        {
            int column, line;
            CurrentContext.Expression = CurrentContext.Expression.TrimStart(out column, out line);
            CurrentContext.Line += line;
            CurrentContext.Column = line > 0 ? column : CurrentContext.Column + column;

            if (string.IsNullOrEmpty(CurrentContext.Expression))
                return false;

            foreach (var kvp in RegexMap)
            {
                var regex = kvp.Value;
                var match = regex.Match(CurrentContext.Expression);
                var value = match.Value;
                if (value.Any())
                {
                    Token = new Token(kvp.Key, ConvertTokenValue(kvp.Key, value), new ParseState(CurrentContext));

                    CurrentContext.Expression = CurrentContext.Expression.Substring(value.Length, out column, out line);
                    CurrentContext.Line += line;
                    CurrentContext.Column = line > 0 ? column : CurrentContext.Column + column;
                    return true;
                }
            }
            throw new ParseErrorException("Invalid token.", CurrentContext);
        }

        private object ConvertTokenValue(TokenType type, string value)
        {
            switch (type)
            {
                case TokenType.NULL:
                    return null;
                case TokenType.INT:
                    return int.Parse(value);
                case TokenType.FLOAT:
                    return double.Parse(value); // by default, treat real numeric literals as 64-bit floating binary point values (as C# does, gives better precision than float)
                case TokenType.BOOL:
                    return bool.Parse(value);
                case TokenType.STRING:
                    return ParseStringLiteral(value);
                default:
                    return value;
            }
        }

        private string ParseStringLiteral(string value)
        {
            //System.Diagnostics.Debug.WriteLine(value); // when looking at expression in debugger, take into account that debugger prints control characters, while output does not
            var unescaped = value.Substring(1, value.Length - 2)
                .Replace(@"\'", "'") // remove backslash escape character when it is placed in front of single quote character 
                                     // (when such a quote is used internally inside string literal, e.g. John\'s cat => changed to John's cat)
                .Replace(@"\n", Environment.NewLine) // in our language \n represents new line for current environment
                .Replace(@"\r", "\r")
                .Replace(@"\t", "\t")
                .Replace(@"\\", "\\");
            return unescaped;
            //System.Diagnostics.Debug.WriteLine(unescaped);
        }
    }
}
