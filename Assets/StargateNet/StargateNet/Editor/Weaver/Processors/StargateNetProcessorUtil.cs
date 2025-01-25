using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace StargateNet
{
    public static class StargateNetProcessorUtil
    {
        // 所有大于4字节的类型才能传输
        public static readonly HashSet<string> NetworkedableTypes = new()
        {
            typeof(StargateNet.NetworkBool).FullName,
            typeof(System.Int32).FullName,
            typeof(System.UInt32).FullName,
            typeof(System.Int64).FullName,
            typeof(System.UInt64).FullName,
            typeof(System.Single).FullName,
            typeof(System.Double).FullName,
            typeof(UnityEngine.Vector2).FullName,
            typeof(UnityEngine.Vector3).FullName,
            typeof(UnityEngine.Vector4).FullName,
            // typeof(System.String).FullName,
        };

        public static int CalculateFieldSize(TypeReference typeReference)
        {
            switch (typeReference.MetadataType)
            {
                case MetadataType.Int32:
                    return sizeof(int);
                case MetadataType.UInt32:
                    return sizeof(uint);
                case MetadataType.Int64:
                    return sizeof(long);
                case MetadataType.UInt64:
                    return sizeof(ulong);
                case MetadataType.Single:
                    return sizeof(float);
                case MetadataType.Double:
                    return sizeof(double);
            }

            switch (typeReference.FullName)
            {
                case "UnityEngine.Vector4":
                    return 4 * sizeof(float);
                case "UnityEngine.Vector3":
                    return 3 * sizeof(float);
                case "UnityEngine.Vector2":
                    return 2 * sizeof(float);
                case "StargateNet.NetworkBool":
                    return sizeof(int);

                default:
                    throw new Exception($"Unsported Type:{typeReference.FullName}");
            }
        }
        
        public static int CalculateFieldSize(TypeDefinition typeDefinition)
        {
            switch (typeDefinition.MetadataType)
            {
                case MetadataType.Int32:
                    return sizeof(int);
                case MetadataType.UInt32:
                    return sizeof(uint);
                case MetadataType.Int64:
                    return sizeof(long);
                case MetadataType.UInt64:
                    return sizeof(ulong);
                case MetadataType.Single:
                    return sizeof(float);
                case MetadataType.Double:
                    return sizeof(double);
            }

            switch (typeDefinition.FullName)
            {
                case "UnityEngine.Vector4":
                    return 4 * sizeof(float);
                case "UnityEngine.Vector3":
                    return 3 * sizeof(float);
                case "UnityEngine.Vector2":
                    return 2 * sizeof(float);
                case "StargateNet.NetworkBool":
                    return sizeof(int);

                default:
                    throw new Exception($"Unsported Type:{typeDefinition.FullName}");
            }
        }
        
        public static bool IsSubclassOf(this TypeDefinition typeDefinition, string ClassTypeFullName)
        {
            if (!typeDefinition.IsClass)
                return false;
            for (TypeReference baseType = typeDefinition.BaseType; baseType != null; baseType = baseType.Resolve().BaseType)
            {
                if (baseType.FullName == ClassTypeFullName)
                    return true;
                if (baseType.Resolve() == null)
                    return false;
            }
            return false;
        }
    }
}