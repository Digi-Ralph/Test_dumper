using BeyondTools.SparkBuffer.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeyondTools.SparkBuffer
{
    public static class SparkManager
    {
        public static readonly JsonSerializerOptions jsonSerializerOptions = new() { IncludeFields = true, WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

        private static readonly Dictionary<int, BeanType> beanTypeMap = [];
        private static readonly Dictionary<int, EnumType> enumTypeMap = [];

        public static BeanType BeanTypeFromHash(int hash)
            => beanTypeMap[hash];

        public static void ReadTypeDefinitions(BinaryReader reader)
        {
            var typeDefCount = reader.ReadInt32();
            while (typeDefCount-- > 0)
            {
                var sparkType = reader.ReadSparkType();
                reader.Align4Bytes();

                if (sparkType == SparkType.Enum)
                {
                    var enumType = new EnumType(reader);
                    enumTypeMap.TryAdd(enumType.typeHash, enumType);
                }
                else if (sparkType == SparkType.Bean)
                {
                    var beanType = new BeanType(reader);
                    beanTypeMap.TryAdd(beanType.typeHash, beanType);
                }
            }
        }
    }
}
