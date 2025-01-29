using BeyondTools.SparkBuffer.Extensions;
using ConsoleAppFramework;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BeyondTools.SparkBuffer
{
    internal class SparkBufferDump
    {
        static void Main(string[] args)
        {
            ConsoleApp.Run(args, (
                [Argument] string tableCfgDir,
                [Argument] string outputDir) =>
            {
                if (!Directory.Exists(tableCfgDir))
                    throw new FileNotFoundException($"{tableCfgDir} isn't a valid directory");
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                var success = 0;
                var processingCount = 0;
                foreach (var tablePath in Directory.EnumerateFiles(tableCfgDir))
                {
                    var fileName = Path.GetFileName(tablePath);

                    Console.WriteLine("Reading {0}...", fileName);
                    processingCount++;

                    using var file = File.OpenRead(tablePath);
                    using var binaryReader = new BinaryReader(file);

                    try
                    {
                        var typeDefOffset = binaryReader.ReadInt32();
                        var rootDefOffset = binaryReader.ReadInt32();
                        var dataOffset = binaryReader.ReadInt32();

                        binaryReader.Seek(typeDefOffset);
                        SparkManager.ReadTypeDefinitions(binaryReader);

                        binaryReader.Seek(rootDefOffset);
                        var rootDef = new BeanType.Field
                        {
                            type = binaryReader.ReadSparkType(),
                            name = binaryReader.ReadSparkBufferString()
                        };

                        if (rootDef.type.IsEnumOrBeanType())
                        {
                            binaryReader.Align4Bytes();
                            rootDef.typeHash = binaryReader.ReadInt32();
                        }
                        if (rootDef.type == SparkType.Map)
                        {
                            rootDef.type2 = binaryReader.ReadSparkType();
                            rootDef.type3 = binaryReader.ReadSparkType();

                            if (rootDef.type2.Value.IsEnumOrBeanType())
                            {
                                binaryReader.Align4Bytes();
                                rootDef.typeHash = binaryReader.ReadInt32();
                            }
                            if (rootDef.type3.Value.IsEnumOrBeanType())
                            {
                                binaryReader.Align4Bytes();
                                rootDef.typeHash2 = binaryReader.ReadInt32();
                            }
                        }

                        binaryReader.Seek(dataOffset);
                        var resultFilePath = Path.Combine(outputDir, $"{rootDef.name}.json");
                        switch (rootDef.type)
                        {
                            case SparkType.Bean:
                                var rootBeanType = SparkManager.BeanTypeFromHash((int)rootDef.typeHash!);
                                var beanDump = ReadBeanAsJObject(binaryReader, rootBeanType);
                                File.WriteAllText(resultFilePath, beanDump!.ToString());
                                break;
                            case SparkType.Map:
                                var mapDump = ReadMapAsJObject(binaryReader, rootDef);
                                File.WriteAllText(resultFilePath, JsonSerializer.Serialize(mapDump, SparkManager.jsonSerializerOptions));
                                break;
                            default:
                                throw new NotSupportedException(string.Format("Unsupported root type {0}", rootDef.type));
                        }

                        Console.WriteLine("Dumped {0} successfully", rootDef.name);
                        success++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error in reading {0}, Error: {1}", fileName, ex.ToString());
                    }
                }

                Console.WriteLine("Dumped {0}/{1}", success, processingCount);
            });
        }

        static JsonObject? ReadMapAsJObject(BinaryReader binaryReader, BeanType.Field typeDef)
        {
            var mapDump = new JsonObject();
            var kvCount = binaryReader.ReadInt32();

            for (int i = 0; i < kvCount; i++)
            {
                var key = typeDef.type2 switch
                {
                    SparkType.String => binaryReader.ReadSparkBufferStringOffset(),
                    SparkType.Int => binaryReader.ReadInt32().ToString(),
                    _ => throw new NotSupportedException(string.Format("Unsupported map key type {0}", typeDef.type2)),
                };
                mapDump[key] = null;
            }

            for (int i = 0; i < kvCount; i++)
            {
                mapDump[i] = typeDef.type3 switch
                {
                    SparkType.Bean => ReadBeanAsJObject(binaryReader, SparkManager.BeanTypeFromHash((int)typeDef.typeHash2!), true),
                    SparkType.String => binaryReader.ReadSparkBufferStringOffset(),
                    SparkType.Int => binaryReader.ReadInt32(),
                    _ => throw new NotSupportedException(string.Format("Unsupported map value type {0}", typeDef.type3)),
                };
            }

            return mapDump;
        }

        static JsonObject? ReadBeanAsJObject(BinaryReader binaryReader, BeanType beanType, bool pointer = false)
        {
            long? pointerOrigin = null;
            if (pointer)
            {
                var beanOffset = binaryReader.ReadInt32();
                if (beanOffset == -1)
                    return null;

                pointerOrigin = binaryReader.BaseStream.Position;
                binaryReader.Seek(beanOffset);
            }

            var dumpObj = new JsonObject();
            
            foreach (var (fieldIndex, beanField) in beanType.fields.Index())
            {
                long? origin = null;
                if (beanField.type == SparkType.Array)
                {
                    var fieldOffset = binaryReader.ReadInt32();
                    if (fieldOffset == -1)
                    {
                        dumpObj[beanField.name] = null;
                        continue;
                    }

                    origin = binaryReader.BaseStream.Position;
                    binaryReader.Seek(fieldOffset);
                }

                switch (beanField.type)
                {
                    case SparkType.Array:
                        var jArray = new JsonArray();

                        var itemCount = binaryReader.ReadInt32();
                        while (itemCount-- > 0)
                        {
                            switch (beanField.type2)
                            {
                                case SparkType.String:
                                    jArray.Add(binaryReader.ReadSparkBufferStringOffset());
                                    break;
                                case SparkType.Bean:
                                    jArray.Add(ReadBeanAsJObject(binaryReader, SparkManager.BeanTypeFromHash((int)beanField.typeHash!), true));
                                    break;
                                case SparkType.Float:
                                    jArray.Add(binaryReader.ReadSingle());
                                    break;
                                case SparkType.Long:
                                    jArray.Add(binaryReader.ReadInt64());
                                    break;
                                case SparkType.Int:
                                case SparkType.Enum:
                                    jArray.Add(binaryReader.ReadInt32());
                                    break;
                                case SparkType.Bool:
                                    jArray.Add(binaryReader.ReadBoolean());
                                    break;
                                default:
                                    throw new NotSupportedException(string.Format("Unsupported array type {0} on bean field, position: {1}", beanField.type2, binaryReader.BaseStream.Position));
                            }
                        }

                        dumpObj[beanField.name] = jArray;
                        break;
                    case SparkType.Int:
                    case SparkType.Enum:
                        dumpObj[beanField.name] = binaryReader.ReadInt32();
                        break;
                    case SparkType.Long:
                        dumpObj[beanField.name] = binaryReader.ReadInt64();
                        break;
                    case SparkType.Float:
                        dumpObj[beanField.name] = binaryReader.ReadSingle();
                        break;
                    case SparkType.Double:
                        dumpObj[beanField.name] = binaryReader.ReadDouble();
                        break;
                    case SparkType.String:
                        dumpObj[beanField.name] = binaryReader.ReadSparkBufferStringOffset();
                        break;
                    case SparkType.Bean:
                        dumpObj[beanField.name] = ReadBeanAsJObject(binaryReader, SparkManager.BeanTypeFromHash((int)beanField.typeHash!), true);
                        break;
                    case SparkType.Bool:
                        dumpObj[beanField.name] = binaryReader.ReadBoolean();
                        if (beanType.fields.Length > fieldIndex + 1 && beanType.fields[fieldIndex + 1].type != SparkType.Bool)
                            binaryReader.Align4Bytes();
                        break;
                    case SparkType.Map:
                        var mapOffset = binaryReader.ReadInt32();
                        var mapOrigin = binaryReader.BaseStream.Position;
                        binaryReader.Seek(mapOffset);
                        dumpObj[beanField.name] = ReadMapAsJObject(binaryReader, beanField);
                        binaryReader.Seek(mapOrigin);
                        break;
                    case SparkType.Byte:
                        throw new Exception(string.Format("Dumping bean field type {0} isn't supported, position: {1}", beanField.type, binaryReader.BaseStream.Position));
                }

                if (origin is not null)
                    binaryReader.BaseStream.Position = (long)origin;
            }

            if (pointerOrigin is not null)
                binaryReader.BaseStream.Position = (long)pointerOrigin;

            return dumpObj;
        }
    }
}
