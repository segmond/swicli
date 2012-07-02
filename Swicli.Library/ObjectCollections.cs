/*  $Id$
*  
*  Project: Swicli.Library - Two Way Interface for .NET and MONO to SWI-Prolog
*  Author:        Douglas R. Miles
*                 Uwe Lesta (SbsSW.SwiPlCs classes)
*  E-mail:        logicmoo@gmail.com
*  WWW:           http://www.logicmoo.com
*  Copyright (C): 2008, Uwe Lesta SBS-Softwaresysteme GmbH, 
*     2010-2012 LogicMOO Developement
*
*  This library is free software; you can redistribute it and/or
*  modify it under the terms of the GNU Lesser General Public
*  License as published by the Free Software Foundation; either
*  version 2.1 of the License, or (at your option) any later version.
*
*  This library is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
*  Lesser General Public License for more details.
*
*  You should have received a copy of the GNU Lesser General Public
*  License along with this library; if not, write to the Free Software
*  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
*
*********************************************************/
#if USE_MUSHDLR
using MushDLR223.Utilities;
#endif
#if USE_IKVM
using jpl;
using Class = java.lang.Class;
#else
using System.Reflection;
using Class = System.Type;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SbsSW.SwiPlCs;

using CycFort = SbsSW.SwiPlCs.PlTerm;
using PrologCli = Swicli.Library.PrologClient;

namespace Swicli.Library
{
    public partial class PrologClient
    {

        private static Type GetColType(object al, out bool isGeneric, out MethodInfo[] reflectCache, out Type elementType)
        {
            elementType = typeof(object);
            var colType = al.GetType();
            isGeneric = colType.IsGenericType;
            if (isGeneric)
            {
                elementType = colType.GetGenericArguments()[0];
            }

            lock (reflectCachesForTypes)
            {
                if (!reflectCachesForTypes.TryGetValue(colType, out reflectCache))
                {
                    reflectCache = reflectCachesForTypes[colType] = new MethodInfo[6];
                }
            }
            return colType;
        }

        private static IEnumerable CreateCollectionOfType(CycFort fromTerm, Type colType)
        {
            if (colType.IsArray)
            {
                return CreateArrayOfType(fromTerm, colType);
            }
            if (!typeof(IEnumerable).IsAssignableFrom(colType))
            {
                Error("Return as collection?", fromTerm);
                return null;
            }
            PlTerm[] terms = ToTermArray(fromTerm);
            int termsLength = terms.Length;
            IEnumerable al = (IEnumerable)MakeDefaultInstance(colType);
            if (termsLength == 0) return al;

            bool isGeneric;
            MethodInfo[] reflectCache;
            Type elementType;
            colType = GetColType(al, out isGeneric, out reflectCache, out elementType);
            if (!isGeneric)
            {
                for (int i = 0; i < termsLength; i++)
                {
                    PlTerm term = terms[i];
                    colAddElementFallback(al, CastTerm(term, elementType), reflectCache);
                }
                return al;
            }
            MethodInfo gMethod = reflectCache[6];
            if (gMethod == null)
            {
                gMethod = reflectCache[6] = typeof(PrologClient).GetMethod("colAddElement", BindingFlagsJustStatic)
                                                .MakeGenericMethod(new[] { elementType });
            }
            for (int i = 0; i < termsLength; i++)
            {
                PlTerm term = terms[i];
                gMethod.Invoke(null, new [] {al, CastTerm(term, elementType), reflectCache});
            }
            return al;
        }
        public static bool cliAddElement(PlTerm enumerable, PlTerm tvalue)
        {
            IEnumerable al = (IEnumerable)CastTerm(enumerable, typeof (IEnumerable));
            bool isGeneric;
            MethodInfo[] reflectCache;
            Type elementType;
            Type colType = GetColType(al, out isGeneric, out reflectCache, out elementType);
            object value = CastTerm(tvalue, elementType);
            if (!isGeneric)
            {
                colAddElementFallback(al, value, reflectCache);
            }
            MethodInfo gMethod = reflectCache[6];
            if (gMethod == null)
            {
                gMethod = reflectCache[6] = typeof(PrologClient).GetMethod("colAddElement", BindingFlagsJustStatic)
                                                .MakeGenericMethod(new[] { elementType });
            }
            gMethod.Invoke(null, new object[] { al, value, reflectCache });
            return true;
        }
        public static bool colAddElement<T>(IEnumerable<T> enumerable, object value, MethodInfo[] reflectCache)
        {
            if (enumerable is ICollection<T>)
            {
                var al = (ICollection<T>)enumerable;
                al.Add((T)value);
                return true;
            }
            return colAddElementFallback(enumerable, value, reflectCache);
        }

        /// <summary>
        /// this is just stuffed with a random MethodInfo
        /// </summary>
        private static MethodInfo MissingMI = null;


        /// <summary>
        /// 0 = SetValue
        /// 1 = Count
        /// 2 = add/append
        /// 3 = GetValue
        /// 4 = removeValue
        /// 5 = removeAll
        /// 6 = cliAddElementGeneric
        /// </summary>

        private static readonly Dictionary<Type, MethodInfo[]> reflectCachesForTypes = new Dictionary<Type, MethodInfo[]>();

        public static string[] setItemMethodNames = new[] { "set_Item", "SetValue", "Set" /*, "Insert", "InsertAt"*/};
        public static string[] countItemMethodNames = new string[] { "Count", "Size", "Length" };
        public static string[] addItemMethodNames = new string[] { "Add", "Append" };

        public static bool colAddElementFallback(IEnumerable enumerable, object value, MethodInfo[] reflectCache)
        {
            if (enumerable is ICollection)
            {
                var al = (ICollection) enumerable;
                return colSetElementFallback(al, new[] {al.Count}, value, reflectCache);
            }
            return AppendReflectively(enumerable.GetType(), enumerable, value, reflectCache);
        }

        public static bool cliSetElement(PlTerm enumerable, PlTerm indexes, PlTerm tvalue)
        {
            IEnumerable al = (IEnumerable) CastTerm(enumerable, typeof (IEnumerable));
            bool isGeneric;
            MethodInfo[] reflectCache;
            Type elementType;
            Type colType = GetColType(al, out isGeneric, out reflectCache, out elementType);
            object value = CastTerm(tvalue, elementType);
            int[] idx = (int[]) CastTerm(indexes, typeof (int[]));
            if (colType.IsArray)
            {
                Array array = (Array)al;
                array.SetValue(value, idx);
                return true;
            }
            if (!isGeneric)
            {
                colSetElementFallback(al, idx, value, reflectCache);
            }
            MethodInfo gMethod = reflectCache[6];
            if (gMethod == null)
            {
                gMethod = reflectCache[6] = typeof (PrologClient).GetMethod("colSetElement", BindingFlagsJustStatic)
                                                .MakeGenericMethod(new[] {elementType});
            }
            gMethod.Invoke(null, new object[] {al, idx, value, reflectCache});
            return true;
        }

        public static bool colSetElement<T>(IEnumerable<T> enumerable, int[] i, T value, MethodInfo[] reflectCache)
        {
            if (enumerable is IList<T>)
            {
                var al = (IList<T>)enumerable;
                al[i[0]] = value;
                return true;
            }
            return colSetElementFallback(enumerable, i, value, reflectCache);
        }

        public static bool colSetElementFallback(IEnumerable enumerable, int[] i, object value, MethodInfo[] reflectCache)
        {

            if (enumerable is Array)
            {
                var al = enumerable as Array;
                al.SetValue(value, i);
                return true;
            }
            if (enumerable is IList)
            {
                var al = enumerable as IList;
                al[i[0]] = value;
                return true;
            }
            // do it all via bad reflection
            ParameterInfo[] pt = null;
            Type type = null;
            if (reflectCache[0] == null) foreach (var mname in setItemMethodNames)
                {
                    type = type ?? enumerable.GetType();
                    MethodInfo mi = type.GetMethod(mname, BindingFlagsInstance);
                    if (mi == null) continue;
                    pt = mi.GetParameters();
                    if (pt.Length != 2) continue;
                    reflectCache[0] = mi;
                    break;
                }

            MethodInfo reflectCache0 = reflectCache[0];
            if (reflectCache0 != null && reflectCache0 != MissingMI)
            {
                pt = pt ?? reflectCache0.GetParameters();
                bool indexIsFirst = false;
                if (pt[0].ParameterType == typeof(int))
                {
                    indexIsFirst = true;
                }
                if (indexIsFirst)
                {
                    reflectCache0.Invoke(enumerable, new object[] { i, value });
                }
                else
                {
                    reflectCache0.Invoke(enumerable, new object[] { value, i });
                }
                return true;
            }

            // get the Count
            if (reflectCache[1] == null) foreach (var mname in countItemMethodNames)
                {

                    type = type ?? enumerable.GetType();
                    MethodInfo mi = type.GetMethod(mname, BindingFlagsInstance);
                    if (mi != null)
                    {
                        reflectCache[1] = mi;
                        break;
                    }
                }
            reflectCache0 = reflectCache[1];
            if (reflectCache0 != null && reflectCache0 != MissingMI)
            {
                return Error("Cant even find Count on {0}", enumerable);
            }
            int size = (int)reflectCache[1].Invoke(enumerable, ZERO_OBJECTS);
            // append elements
            if (i[0] != size)
            {
                return Error("wrong size for element {0} on {1} with count of {2}", i, enumerable, size);
            }
            return AppendReflectively(type, enumerable, value, reflectCache);
        }


        private static bool AppendReflectively(Type type, IEnumerable enumerable, object value, MethodInfo[] reflectCache)
        {
            
            ParameterInfo[] pt;
            MethodInfo reflectCache0 = reflectCache[2];
            if (reflectCache0 == null)
            {
                foreach (var mname in addItemMethodNames)
                {
                    type = type ?? enumerable.GetType();
                    MethodInfo mi = type.GetMethod(mname, BindingFlagsInstance);
                    if (mi == null) continue;
                    pt = mi.GetParameters();
                    if (pt.Length != 1) continue;
                    reflectCache0 = reflectCache[2] = mi;
                    break;
                }               
            }
            if (reflectCache0 != null)
            {
                if (reflectCache0 != MissingMI)
                {
                    reflectCache0.Invoke(enumerable, new object[] { value });
                    return true;
                }
            }
            else
            {
                reflectCache[2] = MissingMI;
            }
            return Error("No append method on {0}", enumerable);
        }
    }

}
