namespace BeyondTools.SparkBuffer.Extensions
{
    public static class SparkTypeExtensions
    {
        public static bool IsEnumOrBeanType(this SparkType type)
            => type is SparkType.Enum or SparkType.Bean;
    }
}
