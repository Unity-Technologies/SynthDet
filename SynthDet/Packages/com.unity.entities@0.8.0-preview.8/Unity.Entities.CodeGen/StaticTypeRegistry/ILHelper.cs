
using System;
using System.Collections.Generic;
#if UNITY_DOTSPLAYER
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Unity.Entities.CodeGen
{
    static class ILHelper
    {
        // Annoyingly cecil's il.Emit API doesn't allow generic operand values so we have a variety of
        // helper functions that all do similar things with only the input list type varying  
        public static void StoreStringArrayInField(ILProcessor il, List<string> values, FieldReference fieldRef, bool isStaticField)
        {
            var stringTypeRef = il.Body.Method.Module.ImportReference(typeof(string));

            PushNewArray(il, stringTypeRef, values.Count);
            // Only need to load true values; false is default.
            for (int i = 0; i < values.Count; ++i)
            {
                PushNewArrayElement(il, i);
                il.Emit(OpCodes.Ldstr, values[i]);
                il.Emit(OpCodes.Stelem_Ref);
            }
            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        public static void StoreBoolArrayInField(ILProcessor il, List<bool> values, FieldReference fieldRef, bool isStaticField)
        {
            var boolTypeDef = il.Body.Method.Module.ImportReference(typeof(bool));

            PushNewArray(il, boolTypeDef, values.Count);
            // Only need to load true values; false is default.
            for (int i = 0; i < values.Count; ++i)
            {
                PushNewArrayElement(il, i);
                il.Emit(values[i] ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stelem_I1);
            }
            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }
        
        public static void StoreIntArrayInField(ILProcessor il, List<int> values, FieldReference fieldRef, bool isStaticField)
        {
            var boolTypeDef = il.Body.Method.Module.ImportReference(typeof(int));

            PushNewArray(il, boolTypeDef, values.Count);
            // Only need to load true values; false is default.
            for (int i = 0; i < values.Count; ++i)
            {
                PushNewArrayElement(il, i);
                EmitLoadConstant(il, values[i]);
                il.Emit(OpCodes.Stelem_I1);
            }
            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }
        
        public static void StoreTopOfStackToField(ILProcessor il, FieldReference fieldRef, bool isStatic)
        {
            if (isStatic)
                il.Emit(OpCodes.Stsfld, fieldRef);
            else
                il.Emit(OpCodes.Stfld, fieldRef);
        }

        public static void PushNewArray(ILProcessor il, TypeReference elementTypeRef, int arraySize)
        {
            EmitLoadConstant(il, arraySize);                    // Push Array Size
            il.Emit(OpCodes.Newarr, elementTypeRef);    // Push array reference to top of stack
        }

        /// <summary>
        /// NOTE: This functions assumes the array is at the top of the stack
        /// </summary>
        public static void PushNewArrayElement(ILProcessor il, int elementIndex)
        {
            il.Emit(OpCodes.Dup);       // Duplicate top of stack (the array)
            EmitLoadConstant(il, elementIndex); // Push array index onto the stack
        }

        public static void EmitLoadConstant(ILProcessor il, int val)
        {
            if (val >= -128 && val < 128)
            {
                switch (val)
                {
                    case -1:
                        il.Emit(OpCodes.Ldc_I4_M1); break;
                    case 0:
                        il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1:
                        il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2:
                        il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3:
                        il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4:
                        il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5:
                        il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6:
                        il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7:
                        il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8:
                        il.Emit(OpCodes.Ldc_I4_8); break;
                    default:
                        il.Emit(OpCodes.Ldc_I4_S, (sbyte)val); break;
                }
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, val);
            }
        }

        public static void EmitLoadConstant(ILProcessor il, long val)
        {
            // III.3.40 ldc.<type> load numeric constant (https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf)
            long absVal = Math.Abs(val);

            // Value is represented in more than 32-bits
            if ((absVal & 0x7FFFFFFF00000000) != 0)
            {
                il.Emit(OpCodes.Ldc_I8, val);
            }
            // Value is represented in 9 - 32 bits
            else if ((absVal & 0xFFFFFF00) != 0)
            {
                il.Emit(OpCodes.Ldc_I4, val);
                il.Emit(OpCodes.Conv_I8);
            }
            else
            {
                EmitLoadConstant(il, (int)val);
                il.Emit(OpCodes.Conv_I8);
            }
        }

        public static void EmitLoadConstant(ILProcessor il, ulong val)
        {
            // III.3.40 ldc.<type> load numeric constant (https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf)

            // Value is represented in more than 32-bits
            if ((val & 0xFFFFFFFF00000000) != 0)
            {
                il.Emit(OpCodes.Ldc_I8, (long)val);
            }
            // Value is represented in 9 - 32 bits
            else if ((val & 0xFFFFFF00) != 0)
            {
                il.Emit(OpCodes.Ldc_I4, (int)val);
            }
            else
            {
                EmitLoadConstant(il, (int)val);
            }
            il.Emit(OpCodes.Conv_U8);
        }
    }
}
#endif