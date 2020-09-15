using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Console = System.Console;

namespace gm
{
    class Program
    {
        static void Main(string[] args)
        {
        
            var entities = MySqlHelper.GetTableInfo(args[0]);
            var sb = new StringBuilder();
            var tableName = args.Length > 1 ? args[1] : null;
            foreach (var info in entities)
            {
                if (tableName != null && !string.Equals(info.Name, tableName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var className = info.Name;


                sb.AppendLine(@$" /// <summary>
            /// {className}数据访问类
            /// </summary>
              public partial class {className}
            {{");
                foreach (var prop in info.PropInfos)
                {
                    sb.AppendLine($@" /// <summary>
             /// {(prop.Comment == "" ? prop.PropName : prop.Comment)}
             /// </summary>
             {(prop.Length>0? "[MaxLength(" + prop.Length+ ")]":"")} 
             public {prop.TypeName}{(prop.IsNull ? " ? " : "")} {prop.PropName} {{ get; set; }}");
                }

                sb.Append('}');
            }

            Console.Write(sb.ToString());
        }
    }

    public class MySqlHelper
    {
        private const string sql = @"select Col.TABLE_NAME as Name,
                                                  Col.COLUMN_NAME as PropName,
                                                  Col.DATA_TYPE as TypeName,
                                                  Col.IS_NULLABLE as IsNull,
                                                  if(Col.COLUMN_KEY='PRI',true,false) as IsPrimary,
                                                  Col.CHARACTER_MAXIMUM_LENGTH as PropLength,
                                                  Col.COLUMN_COMMENT as Comment 
                                                  from information_schema.COLUMNS as Col where TABLE_SCHEMA='{0}' order by Col.TABLE_NAME,Col.ORDINAL_POSITION";

        public static List<TableInfo> GetTableInfo(string sqlConnStr)
        {
            var dbname = Regex.Match(sqlConnStr, "database=.*?;").Value[9..^1];
            List<TableInfo> list = new List<TableInfo>();

            using var conn = new MySqlConnection(sqlConnStr);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(string.Format(sql, dbname), conn);
            MySqlDataReader rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                //string className = ToCamel(rd[0].ToString());
                string className = rd[0].ToString();
                string propName = rd[1].ToString();
                string typeName = GetCLRType(rd[2].ToString());

                bool isNull =string.Equals(rd[3].ToString(),"YES",StringComparison.OrdinalIgnoreCase)|| string.Equals(rd[3].ToString(), "TRUE", StringComparison.OrdinalIgnoreCase);
                if (typeName == "string" || typeName == "byte[]")
                {
                    isNull = false;
                }

                bool isPrimary = Convert.ToBoolean(rd[4]);
                var l = int.TryParse(rd[5].ToString(), out var len);
                int propLength = 0;
                if (l)
                {
                    propLength = len;
                }

                string comment = rd[6].ToString();

                var entity = list.Find(item => item.Name == className);
                if (entity != null)
                {
                    entity.PropInfos.Add(new PropInfo()
                    {
                        PropName = propName,
                        TypeName = typeName,
                        IsNull = isNull,
                        IsPrimary = isPrimary,
                        Comment = comment,
                        Length = propLength
                    });
                }
                else
                {
                    entity = new TableInfo()
                    {
                        Name = className,
                        PropInfos = new List<PropInfo>()
                        {
                            new PropInfo()
                            {
                                PropName = propName,
                                TypeName = typeName,
                                IsNull = isNull,
                                IsPrimary = isPrimary,
                                Comment = comment,
                                Length = propLength
                            }
                        }
                    };
                    list.Add(entity);
                }
            }

            return list;
        }


        /// <summary>
        /// 实体类信息
        /// </summary>
        public class TableInfo
        {
            public string Name { get; set; }
            public List<PropInfo> PropInfos { get; set; }
        }

        /// <summary>
        /// 属性信息
        /// </summary>
        public class PropInfo
        {
            /// <summary>
            /// 属性名称
            /// </summary>
            public string PropName { get; set; }

            /// <summary>
            /// 属性类型
            /// </summary>
            public string TypeName { get; set; }

            /// <summary>
            /// 是否为主键
            /// </summary>
            public bool IsPrimary { get; set; }

            /// <summary>
            /// 是否可空
            /// </summary>
            public bool IsNull { get; set; }

            /// <summary>
            /// 备注信息
            /// </summary>
            public string Comment { get; set; }

            /// <summary>
            /// 长度
            /// </summary>
            public int Length { get; set; }
        }

        private static string ToCamel(string str)
        {
            return str.Split('_').Aggregate("",
                (current, word) =>
                    current + System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(word));
        }


        private static string GetCLRType(string dbType)
        {
            string sysType = "string";
            switch (dbType)
            {
                case "bigint":
                case "bit":
                    sysType = "long";
                    break;
                case "smallint":
                    sysType = "short";
                    break;
                case "int":
                    sysType = "int";
                    break;
                case "uniqueidentifier":
                    sysType = "Guid";
                    break;
                case "smalldatetime":
                case "datetime":
                case "datetime2":
                case "date":
                case "time":
                    sysType = "DateTime";
                    break;
                case "float":
                    sysType = "float";
                    break;
                case "double":
                    sysType = "double";
                    break;
                case "real":
                    sysType = "float";
                    break;
                case "numeric":
                case "smallmoney":
                case "decimal":
                case "money":
                    sysType = "decimal";
                    break;
                case "tinyint":
                    sysType = "int";
                    break;
                case "image":
                case "binary":
                case "varbinary":
                case "timestamp":
                    sysType = "DateTime";
                    break;
                case "geography":
                    sysType = "Microsoft.SqlServer.Types.SqlGeography";
                    break;
                case "geometry":
                    sysType = "Microsoft.SqlServer.Types.SqlGeometry";
                    break;
            }

            return sysType;
        }
    }
}