﻿/* https://github.com/JaroslawWaliszko/ExpressiveAnnotations
 * Copyright (c) 2014 Jaroslaw Waliszko
 * Licensed MIT: http://opensource.org/licenses/MIT */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ExpressiveAnnotations
{
    internal static class Helper
    {
        public static void MakeTypesCompatible(Expression e1, Expression e2, out Expression oute1, out Expression oute2)
        {
            oute1 = e1;
            oute2 = e2;

            // promote numeric values to double - do all computations with higher precision (to be compatible with JavaScript, e.g. notation 1/2, should give 0.5 double not 0 int)
            if (oute1.Type != typeof(double) && oute1.Type != typeof(double?) && oute1.Type.IsNumeric())
                oute1 = oute1.Type.IsNullable()
                    ? Expression.Convert(oute1, typeof (double?))
                    : Expression.Convert(oute1, typeof (double));
            if (oute2.Type != typeof(double) && oute2.Type != typeof(double?) && oute2.Type.IsNumeric())
                oute2 = oute2.Type.IsNullable()
                    ? Expression.Convert(oute2, typeof(double?))
                    : Expression.Convert(oute2, typeof(double));

            // non-nullable operand is converted to nullable if necessary, and the lifted-to-nullable form of the comparison is used (C# rule, which is currently not followed by expression trees)
            if (oute1.Type.IsNullable() && !oute2.Type.IsNullable())
                oute2 = Expression.Convert(oute2, oute1.Type);
            else if (!oute1.Type.IsNullable() && oute2.Type.IsNullable())
                oute1 = Expression.Convert(oute1, oute2.Type);
        }

        public static bool IsDateTime(this Type type)
        {
            return type != null && (type == typeof(DateTime) || (type.IsNullable() && Nullable.GetUnderlyingType(type).IsDateTime()));
        }

        public static bool IsBool(this Type type)
        {
            return type != null && (type == typeof(bool) || (type.IsNullable() && Nullable.GetUnderlyingType(type).IsBool()));
        }

        public static bool IsGuid(this Type type)
        {
            return type != null && (type == typeof(Guid) || (type.IsNullable() && Nullable.GetUnderlyingType(type).IsGuid()));
        }

        public static bool IsString(this Type type)
        {
            return type != null && type == typeof(string);
        }

        public static bool IsNumeric(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            var numericTypes = new HashSet<TypeCode>
            {
                TypeCode.SByte,    //sbyte
                TypeCode.Byte,     //byte
                TypeCode.Int16,    //short
                TypeCode.UInt16,   //ushort
                TypeCode.Int32,    //int
                TypeCode.UInt32,   //uint
                TypeCode.Int64,    //long
                TypeCode.UInt64,   //ulong
                TypeCode.Single,   //float
                TypeCode.Double,   //double
                TypeCode.Decimal   //decimal
            };
            return numericTypes.Contains(Type.GetTypeCode(type)) || type.IsNullable() && Nullable.GetUnderlyingType(type).IsNumeric();
        }

        public static bool IsNullable(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static Type ToNullable(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return typeof (Nullable<>).MakeGenericType(type);
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null) 
                throw new ArgumentNullException("assembly");

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        public static string TrimStart(this string input, out int column, out int line)
        {
            var output = input.TrimStart();
            var redundancy = input.RemoveSuffix(output);
            var lastLineBreak = redundancy.LastIndexOf('\n');
            column = lastLineBreak > 0
                ? redundancy.Length - lastLineBreak
                : input.Length - output.Length;
            line = redundancy.CountLineBreaks();
            return output;
        }

        public static string Substring(this string input, int start, out int column, out int line)
        {
            var output = input.Substring(start);
            var redundancy = input.RemoveSuffix(output);
            var lastLineBreak = redundancy.LastIndexOf('\n');
            column = lastLineBreak > 0
                ? redundancy.Length - lastLineBreak
                : input.Length - output.Length;
            line = redundancy.CountLineBreaks();
            return output;
        }

        public static string RemoveSuffix(this string input, string suffix)
        {
            return input.EndsWith(suffix)
                ? input.Substring(0, input.Length - suffix.Length)
                : input;
        }

        public static int CountLineBreaks(this string input)
        {
            var n = 0;
            for (var i = 0; i < input.Length; i++)
            {                
                if (input[i] == '\n') n++;
            }
            return n;
        }

        public static string FirstLine(this string input)
        {
            return input.Split('\n').First().Trim();
        }

        public static string Indicator(this string input)
        {
            return string.Format(
@"
... {0} ...
    ^--- ", input.FirstLine());
        }

        public static string ToOrdinal(this int num)
        {
            if (num <= 0) 
                return num.ToString(CultureInfo.InvariantCulture);

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }

        }
    }
}
