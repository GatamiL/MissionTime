using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;

namespace MissionTime.Services
{
    public static class DbSchema
    {
        public const int SchemaVersion = 3;
        private static readonly TableDef[] Tables =
        {
            //Храним дату в формате ISO yyyy-MM-dd
            new TableDef(
                "Departments",
                new[]
                {
                    new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                    new ColumnDef("ParentId", "INTEGER"),
                    new ColumnDef("Name", "TEXT", notNull: true),
                    new ColumnDef("ShortName", "TEXT"),
                    new ColumnDef("Level", "INTEGER", notNull: true),
                    new ColumnDef("SortOrder", "INTEGER", notNull: true),
                    new ColumnDef("ResponsibleId", "INTEGER")
                },
                new[]
                {
                    "FOREIGN KEY([ParentId]) REFERENCES [Departments]([Id])",
                    "FOREIGN KEY([ResponsibleId]) REFERENCES [Employees]([Id]) ON DELETE SET NULL"
                }
            ),
            new TableDef(
                "Positions",
                new[]
                {
                    new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                    new ColumnDef("Name", "TEXT", notNull: true)
                },
                new[]
                {
                    "UNIQUE([Name])"
                }
            ),
            new TableDef(
                "ListOfWork",
                new[]
                {
                    new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                    new ColumnDef("ParentId", "INTEGER"),
                    new ColumnDef("Name", "TEXT", notNull: true),
                    new ColumnDef("SpecialCode", "TEXT", notNull: true)
                },
                new[]
                {
                    "FOREIGN KEY([ParentId]) REFERENCES [ListOfWork]([Id])"
                }
            ),
            new TableDef(
                "Programs",
                new[]
                {
                    new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                    new ColumnDef("Name", "TEXT", notNull: true),
                    new ColumnDef("ShortName", "TEXT", notNull: true),
                    new ColumnDef("DateStart", "TEXT", notNull: true),
                    new ColumnDef("DateEnd", "TEXT")
                }
            ),
            new TableDef(
                "Employees",
                new[]
                {
                    new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                    new ColumnDef("Fio", "TEXT", notNull: true)
                }
            ),
            new TableDef(
                "EmployeePositionsHistory",
                new[]
                {
                    new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),

                    new ColumnDef("EmployeeId", "INTEGER", notNull: true),
                    new ColumnDef("DepartmentId", "INTEGER", notNull: true),
                    new ColumnDef("PositionId", "INTEGER", notNull: true),
                    new ColumnDef("StartDate", "TEXT", notNull: true),
                    new ColumnDef("Action", "INTEGER", notNull: true),
                    new ColumnDef("Note", "TEXT")                       // Комментарий (например, номер приказа)
                },
                new[]
                {
                    "FOREIGN KEY([EmployeeId]) REFERENCES [Employees]([Id]) ON DELETE RESTRICT",
                    "FOREIGN KEY([DepartmentId]) REFERENCES [Departments]([Id]) ON DELETE RESTRICT",
                    "FOREIGN KEY([PositionId]) REFERENCES [Positions]([Id]) ON DELETE RESTRICT",
                    "CHECK([Action] IN (1,2,3))"
                }
            ),
                new TableDef(
                    "Timesheet",
                    new[]
                    {
                        new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                        new ColumnDef("EmployeePositionsHistoryId", "INTEGER", notNull: true), // КТО
                        new ColumnDef("ProgramId", "INTEGER", notNull: true),                   // ГДЕ (Проект)
                        new ColumnDef("Year", "INTEGER", notNull: true),                        // КОГДА (Год)
                        new ColumnDef("Month", "INTEGER", notNull: true),                       // КОГДА (Месяц)
                        new ColumnDef("CreatedAt", "TEXT", notNull: true)
                    },
                    new[]
                    {
                        "FOREIGN KEY([EmployeePositionsHistoryId]) REFERENCES [EmployeePositionsHistory]([Id]) ON DELETE RESTRICT",
                        "FOREIGN KEY([ProgramId]) REFERENCES [Programs]([Id]) ON DELETE RESTRICT",
                        "UNIQUE([EmployeePositionsHistoryId], [ProgramId], [Year], [Month])"
                    }
                ),
                    new TableDef(
                        "TimesheetEntry",
                        new[]
                        {
                            new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                            new ColumnDef("TimesheetId", "INTEGER", notNull: true), // Ссылка на заголовок
                            new ColumnDef("WorkId", "INTEGER", notNull: true),      // Вид работы
                            new ColumnDef("WorkDate", "TEXT", notNull: true),       // Дата (ISO)
                            new ColumnDef("Minutes", "INTEGER", notNull: true)      // Часы в минутах
                        },
                        new[]
                        {
                            "FOREIGN KEY([TimesheetId]) REFERENCES [Timesheet]([Id]) ON DELETE CASCADE",
                            "FOREIGN KEY([WorkId]) REFERENCES [ListOfWork]([Id]) ON DELETE RESTRICT",
                            "UNIQUE([TimesheetId], [WorkId], [WorkDate])"
                        }
                    ),
            new TableDef(
                "EmployeeSign",
                new[]
                {
                    new ColumnDef("Id", "INTEGER", notNull: true, pk: true, autoinc: true),
                    new ColumnDef("EmployeeId", "INTEGER", notNull: true),
                    new ColumnDef("ImageData", "BLOB"),
                    new ColumnDef("OffsetX", "REAL", notNull: true, defaultValue: "0"),
                    new ColumnDef("OffsetY", "REAL", notNull: true, defaultValue: "0"),
                    new ColumnDef("Scale", "REAL", notNull: true, defaultValue: "1.0")
                },
                new[]
                {
                    "FOREIGN KEY([EmployeeId]) REFERENCES [Employees]([Id]) ON DELETE CASCADE",
                    "UNIQUE([EmployeeId])"
                }
            ),
        };
        public static string GetConnectionString(string dbPath)
            => $"Data Source={dbPath};Version=3;Foreign Keys=True;";
        public static void CreateNew(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is empty.");

            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(dbPath))
                SQLiteConnection.CreateFile(dbPath);

            using (var conn = new SQLiteConnection(GetConnectionString(dbPath)))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    Exec(conn, "PRAGMA foreign_keys = ON;");
                    Exec(conn, @"CREATE TABLE IF NOT EXISTS __SchemaInfo(
                                    SchemaVersion INTEGER NOT NULL
                                 );");

                    Exec(conn, @"DELETE FROM __SchemaInfo;");
                    Exec(conn, @"INSERT INTO __SchemaInfo(SchemaVersion) VALUES (@v);",
                        ("@v", SchemaVersion));

                    foreach (var t in Tables)
                        Exec(conn, t.BuildCreateSql());
                    // --- Индексы для справочников ---
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_Departments_ParentId ON Departments(ParentId);");
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_ListOfWork_ParentId ON ListOfWork(ParentId);");

                    // --- Индексы для истории должностей ---
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_EPH_EmployeeId ON EmployeePositionsHistory(EmployeeId);");
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_EPH_DepartmentId ON EmployeePositionsHistory(DepartmentId);");
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_EPH_Date ON EmployeePositionsHistory(StartDate);");

                    // --- Индексы для ТАБЕЛЯ (Заголовок) ---
                    // Помогает быстро найти табель чела по программе и дате
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_TS_Main ON Timesheet(EmployeePositionsHistoryId, ProgramId, Year, Month);");

                    // --- Индексы для ЗАПИСЕЙ табеля (Детализация) ---
                    // Помогает быстро достать все часы по конкретному ID табеля
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_TSE_TimesheetId ON TimesheetEntry(TimesheetId);");
                    // Помогает быстро собрать данные для сетки по датам
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_TSE_Date ON TimesheetEntry(WorkDate);");
                    // Ускоряет поиск конкретного вида работы
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_TSE_WorkId ON TimesheetEntry(WorkId);");
                    
                    // --- Индексы для ПОДПИСЕЙ ---
                    Exec(conn, "CREATE INDEX IF NOT EXISTS IX_ESign_EmpId ON EmployeeSign(EmployeeId);");

                    tx.Commit();
                }
            }
        }
        public static bool Validate(string dbPath, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(dbPath))
            {
                error = "Путь пустой.";
                return false;
            }

            if (!File.Exists(dbPath))
            {
                error = "Файл не найден.";
                return false;
            }

            try
            {
                using (var fs = File.OpenRead(dbPath))
                {
                    byte[] header = new byte[16];
                    if (fs.Read(header, 0, header.Length) != header.Length)
                    {
                        error = "Файл слишком маленький.";
                        return false;
                    }

                    var signature = System.Text.Encoding.ASCII.GetString(header);
                    if (!signature.StartsWith("SQLite format 3"))
                    {
                        error = "Это не SQLite файл (нет сигнатуры 'SQLite format 3').";
                        return false;
                    }
                }

                using (var conn = new SQLiteConnection(GetConnectionString(dbPath)))
                {
                    conn.Open();

                    if (!TableExists(conn, "__SchemaInfo"))
                    {
                        error = "Нет таблицы __SchemaInfo (не наша БД).";
                        return false;
                    }

                    var verObj = Scalar(conn, "SELECT SchemaVersion FROM __SchemaInfo LIMIT 1;");
                    if (verObj == null || verObj == DBNull.Value)
                    {
                        error = "Не найдена версия схемы.";
                        return false;
                    }

                    int ver = Convert.ToInt32(verObj);
                    if (ver < SchemaVersion)
                    {
                        // --- АВТОМАТИЧЕСКАЯ МИГРАЦИЯ ПРИ ЗАПУСКЕ ---
                        using (var tx = conn.BeginTransaction())
                        {
                            // БЕЗОПАСНОСТЬ: Если таблица создалась в черновике версии 2,
                            // но без новых полей Scale/Offset — мы ее пересоздадим.
                            // Там 100% нет пользовательских данных еще.
                            if (ver < 3)
                            {
                                // Попытка добавить колонки, если таблица существует
                                if (TableExists(conn, "EmployeeSign"))
                                {
                                    // Получаем информацию о колонках
                                    var actual = ReadTableInfo(conn, "EmployeeSign");
                                    if (!actual.ContainsKey("OffsetX"))
                                        Exec(conn, "ALTER TABLE EmployeeSign ADD COLUMN OffsetX REAL NOT NULL DEFAULT 0;");
                                    if (!actual.ContainsKey("OffsetY"))
                                        Exec(conn, "ALTER TABLE EmployeeSign ADD COLUMN OffsetY REAL NOT NULL DEFAULT 0;");
                                    if (!actual.ContainsKey("Scale"))
                                        Exec(conn, "ALTER TABLE EmployeeSign ADD COLUMN Scale REAL NOT NULL DEFAULT 1.0;");
                                }
                            }

                            // Создаем все новые таблицы, которых не было в старой версии
                            foreach (var t in Tables)
                                Exec(conn, t.BuildCreateSql());

                            // Добавляем новые индексы (если они не создаются внутри BuildCreateSql)
                            Exec(conn, "CREATE INDEX IF NOT EXISTS IX_ESign_EmpId ON EmployeeSign(EmployeeId);");

                            // Обновляем версию в БД
                            Exec(conn, "UPDATE __SchemaInfo SET SchemaVersion = @v;", ("@v", SchemaVersion));
                            
                            tx.Commit();
                        }
                        ver = SchemaVersion; // Теперь версия совпадает, валидация пройдет успешно
                    }
                    else if (ver > SchemaVersion)
                    {
                        error = $"Версия схемы БД ({ver}) новее, чем поддерживает приложение ({SchemaVersion}). Пожалуйста, обновите приложение.";
                        return false;
                    }

                    foreach (var t in Tables)
                    {
                        if (!TableExists(conn, t.Name))
                        {
                            error = $"Нет таблицы: {t.Name}.";
                            return false;
                        }

                        var actualCols = ReadTableInfo(conn, t.Name);
                        foreach (var expected in t.Columns)
                        {
                            if (!actualCols.TryGetValue(expected.Name, out var actual))
                            {
                                error = $"В таблице {t.Name} нет поля {expected.Name}.";
                                return false;
                            }

                            if (!TypeCompatible(actual.Type, expected.Type))
                            {
                                error = $"Поле {t.Name}.{expected.Name} имеет тип '{actual.Type}', ожидали '{expected.Type}'.";
                                return false;
                            }

                            if (expected.NotNull && !expected.Pk && actual.NotNull != 1)
                            {
                                error = $"Поле {t.Name}.{expected.Name} должно быть NOT NULL.";
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Ошибка при проверке БД: " + ex.Message;
                return false;
            }
        }
        private static void Exec(SQLiteConnection conn, string sql, params (string name, object val)[] prms)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (var p in prms)
                    cmd.Parameters.AddWithValue(p.name, p.val ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
        private static object Scalar(SQLiteConnection conn, string sql)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                return cmd.ExecuteScalar();
            }
        }
        private static bool TableExists(SQLiteConnection conn, string tableName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@n LIMIT 1;";
                cmd.Parameters.AddWithValue("@n", tableName);
                var r = cmd.ExecuteScalar();
                return r != null;
            }
        }
        private static Dictionary<string, TableInfoRow> ReadTableInfo(SQLiteConnection conn, string tableName)
        {
            var dict = new Dictionary<string, TableInfoRow>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info([{tableName}]);";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        // pragma columns: cid, name, type, notnull, dflt_value, pk
                        var name = r["name"].ToString();
                        var type = (r["type"]?.ToString() ?? "").Trim();
                        var notnull = Convert.ToInt32(r["notnull"]);

                        dict[name] = new TableInfoRow { Type = type, NotNull = notnull };
                    }
                }
            }

            return dict;
        }
        private static bool TypeCompatible(string actual, string expected)
        {
            actual = (actual ?? "").Trim().ToUpperInvariant();
            expected = (expected ?? "").Trim().ToUpperInvariant();

            if (actual == expected) return true;
            if (actual.StartsWith(expected)) return true;

            if (expected == "INTEGER" && actual.Contains("INT")) return true;
            if (expected == "TEXT" && (actual.Contains("CHAR") || actual.Contains("CLOB") || actual.Contains("TEXT"))) return true;
            if (expected == "REAL" && (actual.Contains("REAL") || actual.Contains("FLOA") || actual.Contains("DOUB"))) return true;
            if (expected == "BLOB" && actual.Contains("BLOB")) return true;
            if (expected == "NUMERIC" && (actual.Contains("NUM") || actual.Contains("DEC") || actual.Contains("BOOL") || actual.Contains("DATE"))) return true;

            return false;
        }
        private sealed class TableDef
        {
            public string Name { get; }
            public ColumnDef[] Columns { get; }
            public string[] Constraints { get; }

            public TableDef(string name, ColumnDef[] cols, string[] constraints = null)
            {
                Name = name;
                Columns = cols ?? Array.Empty<ColumnDef>();
                Constraints = constraints ?? Array.Empty<string>();
            }

            public string BuildCreateSql()
            {
                var parts = new List<string>();

                foreach (var c in Columns)
                    parts.Add(c.BuildSql());

                foreach (var k in Constraints)
                    parts.Add(k);

                return $"CREATE TABLE IF NOT EXISTS [{Name}] (\n  {string.Join(",\n  ", parts)}\n);";
            }
        }
        private sealed class ColumnDef
        {
            public string Name { get; }
            public string Type { get; }
            public bool NotNull { get; }
            public bool Pk { get; }
            public bool AutoInc { get; }
            public string DefaultValue { get; }

            public ColumnDef(string name, string type, bool notNull = false, bool pk = false, bool autoinc = false, string defaultValue = null)
            {
                Name = name;
                Type = type;
                NotNull = notNull;
                Pk = pk;
                AutoInc = autoinc;
                DefaultValue = defaultValue;
            }

            public string BuildSql()
            {
                if (Pk && AutoInc)
                    return $"[{Name}] INTEGER PRIMARY KEY AUTOINCREMENT";

                var sql = $"[{Name}] {Type}";
                if (NotNull) sql += " NOT NULL";
                if (Pk) sql += " PRIMARY KEY";
                if (DefaultValue != null) sql += $" DEFAULT {DefaultValue}";
                return sql;
            }

        }
        public enum DepartmentLevel
        {
            Center = 1,
            Complex = 2,
            Department = 3,
            Group = 4
        }
        private sealed class TableInfoRow
        {
            public string Type { get; set; }
            public int NotNull { get; set; }
        }
        public enum EmployeeAction
        {
            Hire = 1,
            Transfer = 2,
            Fire = 3
        }
    }
}
