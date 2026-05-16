using MissionTime.Models;
using MissionTime.Views;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.IO;

namespace MissionTime.Services
{
    public class DbService
    {
        #region Состояние и свойства
        public string DbPath { get; private set; }
        public string ConnectionString { get; private set; }
        public bool IsConnected => !string.IsNullOrWhiteSpace(ConnectionString);
        #endregion
        #region Инициализация и подключение
        public DbService(string dbPath)
        {
            Connect(dbPath);
        }
        public void Connect(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is empty.");

            if (!DbSchema.Validate(dbPath, out var err))
                throw new InvalidOperationException("DB schema validation failed: " + err);

            TryAutoBackup(dbPath); // Делаем резервную копию при старте!
            
            DbPath = dbPath;
            ConnectionString = DbSchema.GetConnectionString(dbPath);

            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1;";
                cmd.ExecuteScalar();
            }
        }

        private void TryAutoBackup(string dbPath)
        {
            try
            {
                if (!File.Exists(dbPath)) return;

                string dir = Path.GetDirectoryName(dbPath);
                string backupDir = Path.Combine(dir, "Backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string ts = DateTime.Now.ToString("yyyyMMdd");
                string fileName = $"Backup_{ts}.db"; // По одному бэкапу в день!
                string targetPath = Path.Combine(backupDir, fileName);

                // Если сегодня бэкап уже был — не мучаем диск повторно
                if (File.Exists(targetPath)) return;

                File.Copy(dbPath, targetPath, true);

                // ЧИСТКА: Оставляем только последние 10 копий, остальное удаляем
                var backups = Directory.GetFiles(backupDir, "Backup_*.db")
                                       .OrderByDescending(f => f)
                                       .Skip(10)
                                       .ToList();
                foreach (var old in backups)
                {
                    try { File.Delete(old); } catch { }
                }
            }
            catch (Exception ex)
            {
                LogService.Log("Ошибка создания автобэкапа:", ex);
            }
        }

        public SQLiteConnection OpenConnection()
        {
            if (!IsConnected)
                throw new InvalidOperationException("DB is not connected. Call Connect(dbPath) first.");

            var conn = new SQLiteConnection(ConnectionString);
            conn.Open();
            return conn;
        }
        #endregion
        #region Выполнение запросов (CRUD)
        public int Execute(string sql, params SQLiteParameter[] parameters)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);

                return cmd.ExecuteNonQuery();
            }
        }
        public object Scalar(string sql, params SQLiteParameter[] parameters)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);

                return cmd.ExecuteScalar();
            }
        }
        public DataTable Query(string sql, params SQLiteParameter[] parameters)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);

                using (var da = new SQLiteDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    da.Fill(dt);
                    return dt;
                }
            }
        }
        #endregion
        #region Работа с транзакциями
        public void InTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            using (var conn = OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                action(conn, tx);
                tx.Commit();
            }
        }
        public T InTransactionReturn<T>(Func<SQLiteConnection, SQLiteTransaction, T> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            using (var conn = OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    T result = action(conn, tx);
                    tx.Commit();
                    return result;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }
        #endregion
        #region Positions
        public DataTable Positions_List()
        {
            return Query("SELECT Id, Name FROM Positions ORDER BY Name;");
        }
        public long Position_Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("name empty");

            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO Positions(Name) VALUES(@n);
                            SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@n", name.Trim());
                long id = (long)cmd.ExecuteScalar();
                LogService.Log($"ДОЛЖНОСТЬ СОЗДАНА: '{name.Trim()}' (ID: {id})");
                return id;
            }
        }
        public int Position_Update(long id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("name empty");

            int res = Execute(
                "UPDATE Positions SET Name=@n WHERE Id=@id;",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@id", id)
            );
            LogService.Log($"ДОЛЖНОСТЬ ИЗМЕНЕНА (ID: {id}): Новое имя '{name.Trim()}'");
            return res;
        }
        public int Position_Delete(long id)
        {
            int res = Execute(
                "DELETE FROM Positions WHERE Id=@id;",
                new SQLiteParameter("@id", id)
            );
            LogService.Log($"ДОЛЖНОСТЬ УДАЛЕНА (ID: {id})");
            return res;
        }
        #endregion
        #region Departments
        public DataTable Departments_List()
        {
            return Query(@"
                SELECT 
                    d.Id, d.ParentId, d.Name, d.ShortName, d.Level, d.ResponsibleId,
                    e.Fio AS ResponsibleFio
                FROM Departments d
                LEFT JOIN Employees e ON d.ResponsibleId = e.Id
                ORDER BY d.Level, d.SortOrder, d.Name;");
        }

        public long Department_Create(string name, string shortName, long? parentId, long? responsibleId, int level)
        {
            // Расчет SortOrder: просто берем макс + 1 для этого уровня
            object maxSort = Scalar("SELECT MAX(SortOrder) FROM Departments WHERE ParentId IS @p",
                                    new SQLiteParameter("@p", parentId));
            int nextSort = (maxSort == DBNull.Value) ? 1 : Convert.ToInt32(maxSort) + 1;

            long id = (long)Scalar(@"
                INSERT INTO Departments (Name, ShortName, ParentId, ResponsibleId, Level, SortOrder) 
                VALUES (@n, @sn, @p, @r, @l, @s);
                SELECT last_insert_rowid();",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@sn", shortName?.Trim()),
                new SQLiteParameter("@p", parentId),
                new SQLiteParameter("@r", responsibleId),
                new SQLiteParameter("@l", level),
                new SQLiteParameter("@s", nextSort));

            LogService.Log($"ПОДРАЗДЕЛЕНИЕ СОЗДАНО: '{name.Trim()}' (ID: {id})");
            return id;
        }

        // Список сотрудников для выбора ответственного
        public DataTable Employees_List_Brief()
        {
            return Query("SELECT Id, Fio FROM Employees ORDER BY Fio;");
        }
        public void Department_Update(long id, string name, string shortName, long? responsibleId)
        {
            Execute(@"
        UPDATE Departments 
        SET Name = @n, ShortName = @sn, ResponsibleId = @r 
        WHERE Id = @id",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@sn", shortName?.Trim()),
                new SQLiteParameter("@r", responsibleId),
                new SQLiteParameter("@id", id));

            LogService.Log($"ПОДРАЗДЕЛЕНИЕ ИЗМЕНЕНО (ID: {id}): '{name.Trim()}'");
        }
        public void Department_Delete(long id)
        {
            Execute("DELETE FROM Departments WHERE Id = @id", new SQLiteParameter("@id", id));
            LogService.Log($"ПОДРАЗДЕЛЕНИЕ УДАЛЕНО (ID: {id})");
        }
        #endregion
        #region Employees & History
        public DataTable Employees_List_Full(bool showFired)
        {
            string sql = @"
        SELECT e.Id, e.Fio, p.Name as PositionName, d.Name as DepartmentName, h.Action
        FROM Employees e
        JOIN EmployeePositionsHistory h ON h.EmployeeId = e.Id
        JOIN Positions p ON h.PositionId = p.Id
        JOIN Departments d ON h.DepartmentId = d.Id
        WHERE h.Id = (SELECT MAX(Id) FROM EmployeePositionsHistory WHERE EmployeeId = e.Id)";

            if (!showFired) sql += " AND h.Action != 3";
            sql += " ORDER BY e.Fio";

            return Query(sql);
        }
        public void Employee_Create(string fio, long depId, long posId)
        {
            InTransaction((conn, tx) =>
            {
                // 1. Создаем сотрудника
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO Employees (Fio) VALUES (@fio); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@fio", fio.Trim());
                    long empId = Convert.ToInt64(cmd.ExecuteScalar());

                    // 2. Создаем запись в историю
                    cmd.CommandText = @"
                INSERT INTO EmployeePositionsHistory (EmployeeId, DepartmentId, PositionId, StartDate, Action)
                VALUES (@eid, @did, @pid, @date, 1)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@eid", empId);
                    cmd.Parameters.AddWithValue("@did", depId);
                    cmd.Parameters.AddWithValue("@pid", posId);
                    cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.ExecuteNonQuery();

                    LogService.Log($"СОТРУДНИК ПРИНЯТ: '{fio.Trim()}' (ID: {empId}), Подразделение ID: {depId}, Должность ID: {posId}");
                }
            });
        }
        public void Employee_Transfer(long empId, long depId, long posId, string note, string date)
        {
            Execute(@"
        INSERT INTO EmployeePositionsHistory (EmployeeId, DepartmentId, PositionId, StartDate, Action, Note)
        VALUES (@eid, @did, @pid, @date, 2, @note)",
                new SQLiteParameter("@eid", empId),
                new SQLiteParameter("@did", depId),
                new SQLiteParameter("@pid", posId),
                new SQLiteParameter("@date", date), // Используем переданную дату
                new SQLiteParameter("@note", note));

            LogService.Log($"СОТРУДНИК ПЕРЕВЕДЕН (ID: {empId}): Подразделение ID: {depId}, Должность ID: {posId}, Дата: {date}");
        }
        public void Employee_Fire(long empId)
        {
            DataTable dt = Query(@"
        SELECT DepartmentId, PositionId FROM EmployeePositionsHistory 
        WHERE EmployeeId = @id ORDER BY Id DESC LIMIT 1",
                new SQLiteParameter("@id", empId));

            if (dt.Rows.Count > 0)
            {
                Execute(@"
            INSERT INTO EmployeePositionsHistory (EmployeeId, DepartmentId, PositionId, StartDate, Action)
            VALUES (@eid, @did, @pid, @date, 3)",
                    new SQLiteParameter("@eid", empId),
                    new SQLiteParameter("@did", dt.Rows[0]["DepartmentId"]),
                    new SQLiteParameter("@pid", dt.Rows[0]["PositionId"]),
                    new SQLiteParameter("@date", DateTime.Now.ToString("yyyy-MM-dd")));

                LogService.Log($"СОТРУДНИК УВОЛЕН (ID: {empId})");
            }
        }
        public DataTable Employee_GetHistory(long empId)
        {
            return Query(@"
        SELECT h.StartDate, d.Name as DepName, p.Name as PosName, h.Action, h.Note
        FROM EmployeePositionsHistory h
        JOIN Departments d ON h.DepartmentId = d.Id
        JOIN Positions p ON h.PositionId = p.Id
        WHERE h.EmployeeId = @id
        ORDER BY h.StartDate DESC, h.Id DESC",
                new SQLiteParameter("@id", empId));
        }

        public void Employee_UndoLastHistory(long empId)
        {
            long count = Convert.ToInt64(Scalar("SELECT COUNT(*) FROM EmployeePositionsHistory WHERE EmployeeId = @id", new SQLiteParameter("@id", empId)));
            if (count <= 1)
            {
                throw new Exception("Нельзя отменить последнюю запись в истории, так как она единственная (запись о приеме на работу).");
            }

            Execute(@"DELETE FROM EmployeePositionsHistory 
                      WHERE Id = (SELECT MAX(Id) FROM EmployeePositionsHistory WHERE EmployeeId = @id)",
                    new SQLiteParameter("@id", empId));

            LogService.Log($"ОТКАТ ИСТОРИИ СОТРУДНИКА (ID: {empId}): Удалена последняя запись истории.");
        }

        public DataTable GetExportDataForPeriod(int year, int month)
        {
            string sql = @"
                SELECT 
                    e.Fio AS EmployeeFio,
                    p.Name AS PositionName,
                    d.Name AS DepartmentName,
                    prog.Name AS ProgramName,
                    lw.SpecialCode AS WorkCode,
                    lw.Name AS WorkName,
                    te.WorkDate,
                    te.Minutes
                FROM TimesheetEntry te
                JOIN Timesheet ts ON te.TimesheetId = ts.Id
                JOIN EmployeePositionsHistory h ON ts.EmployeePositionsHistoryId = h.Id
                JOIN Employees e ON h.EmployeeId = e.Id
                JOIN Positions p ON h.PositionId = p.Id
                JOIN Departments d ON h.DepartmentId = d.Id
                JOIN Programs prog ON ts.ProgramId = prog.Id
                LEFT JOIN ListOfWork lw ON te.WorkId = lw.Id
                WHERE ts.Year = @year AND ts.Month = @month";

            return Query(sql, 
                new SQLiteParameter("@year", year), 
                new SQLiteParameter("@month", month));
        }

        public long? FindEphIdByName(string fio, int year, int month)
        {
            string lastDayStr = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");
            string sql = @"
                SELECT h.Id 
                FROM EmployeePositionsHistory h
                JOIN Employees e ON h.EmployeeId = e.Id
                WHERE e.Fio = @fio AND h.StartDate <= @lastDay
                ORDER BY h.Id DESC LIMIT 1";
            object res = Scalar(sql, new SQLiteParameter("@fio", fio.Trim()), new SQLiteParameter("@lastDay", lastDayStr));
            return res != null ? (long?)Convert.ToInt64(res) : null;
        }

        public long? FindProgramIdByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            object res = Scalar("SELECT Id FROM Programs WHERE Name = @n OR ShortName = @n LIMIT 1", new SQLiteParameter("@n", name.Trim()));
            return res != null ? (long?)Convert.ToInt64(res) : null;
        }

        public long? FindWorkIdByCodeOrName(string code, string name)
        {
            object res = null;
            if (!string.IsNullOrWhiteSpace(code))
                res = Scalar("SELECT Id FROM ListOfWork WHERE SpecialCode = @c LIMIT 1", new SQLiteParameter("@c", code.Trim()));
            
            if (res == null && !string.IsNullOrWhiteSpace(name))
                res = Scalar("SELECT Id FROM ListOfWork WHERE Name = @n LIMIT 1", new SQLiteParameter("@n", name.Trim()));

            return res != null ? (long?)Convert.ToInt64(res) : null;
        }
        #endregion
        #region ListOfWork
        public DataTable ListOfWork_List()
        {
            return Query("SELECT Id, ParentId, Name, SpecialCode FROM ListOfWork ORDER BY Name;");
        }

        public long Work_Create(string name, string code, long? parentId)
        {
            long id = (long)Scalar(@"
        INSERT INTO ListOfWork (Name, SpecialCode, ParentId) 
        VALUES (@n, @c, @p);
        SELECT last_insert_rowid();",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@c", code?.Trim() ?? ""),
                new SQLiteParameter("@p", parentId));

            LogService.Log($"ВИД РАБОТЫ СОЗДАН: '{name.Trim()}' [{code?.Trim()}] (ID: {id})");
            return id;
        }

        public void Work_Update(long id, string name, string code)
        {
            Execute("UPDATE ListOfWork SET Name=@n, SpecialCode=@c WHERE Id=@id",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@c", code.Trim()),
                new SQLiteParameter("@id", id));
            
            LogService.Log($"ВИД РАБОТЫ ИЗМЕНЕН (ID: {id}): '{name.Trim()}' [{code.Trim()}]");
        }

        public void Work_Delete(long id)
        {
            // Проверка: есть ли подпункты
            long count = (long)Scalar("SELECT COUNT(*) FROM ListOfWork WHERE ParentId=@id",
                new SQLiteParameter("@id", id));
            if (count > 0) throw new Exception("Нельзя удалить категорию, пока в ней есть подпункты!");

            Execute("DELETE FROM ListOfWork WHERE Id=@id", new SQLiteParameter("@id", id));
            LogService.Log($"ВИД РАБОТЫ УДАЛЕН (ID: {id})");
        }
        #endregion
        #region Programs
        public DataTable Programs_List()
        {
            // Запрос к твоей таблице Programs
            return Query("SELECT Id, Name, ShortName, DateStart, DateEnd FROM Programs ORDER BY Name;");
        }

        public void Program_Create(string name, string shortName, string dateStart, string dateEnd)
        {
            Execute(@"INSERT INTO Programs (Name, ShortName, DateStart, DateEnd) 
              VALUES (@n, @sn, @ds, @de);",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@sn", shortName.Trim()),
                new SQLiteParameter("@ds", dateStart),
                new SQLiteParameter("@de", string.IsNullOrEmpty(dateEnd) ? (object)DBNull.Value : dateEnd));

            LogService.Log($"ПРОГРАММА СОЗДАНА: '{name.Trim()}' ({shortName.Trim()})");
        }

        public void Program_Update(long id, string name, string shortName, string dateStart, string dateEnd)
        {
            Execute(@"UPDATE Programs 
              SET Name=@n, ShortName=@sn, DateStart=@ds, DateEnd=@de 
              WHERE Id=@id",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@sn", shortName.Trim()),
                new SQLiteParameter("@ds", dateStart),
                new SQLiteParameter("@de", string.IsNullOrEmpty(dateEnd) ? (object)DBNull.Value : dateEnd),
                new SQLiteParameter("@id", id));

            LogService.Log($"ПРОГРАММА ИЗМЕНЕНА (ID: {id}): '{name.Trim()}' ({shortName.Trim()})");
        }

        public void Program_Delete(long id)
        {
            Execute("DELETE FROM Programs WHERE Id=@id", new SQLiteParameter("@id", id));
            LogService.Log($"ПРОГРАММА УДАЛЕНА (ID: {id})");
        }

        public System.Collections.Generic.List<DateTime> GetLoggedDatesForProgram(long progId)
        {
            var dates = new System.Collections.Generic.List<DateTime>();
            string sql = @"
                SELECT DISTINCT te.WorkDate 
                FROM TimesheetEntry te
                JOIN Timesheet ts ON te.TimesheetId = ts.Id
                WHERE ts.ProgramId = @progId AND te.Minutes > 0";
            
            var dt = Query(sql, new SQLiteParameter("@progId", progId));
            foreach(DataRow r in dt.Rows)
            {
                if(r["WorkDate"] != DBNull.Value)
                {
                    if (DateTime.TryParse(r["WorkDate"].ToString(), out DateTime d))
                    {
                        dates.Add(d);
                    }
                }
            }
            return dates;
        }
        #endregion
        public DataTable GetTimeSheetData(long departmentId, int year, int month, long programId)
        {
            // Запрос к твоей таблице Employees (поле Fio)
            string sql = @"
        SELECT 
            e.Id as EmployeeId,
            e.Fio as FullName,
            p.Name as PositionName
        FROM Employees e
        JOIN EmployeePositionsHistory h ON e.Id = h.EmployeeId
        JOIN Positions p ON h.PositionId = p.Id
        WHERE h.Id = (SELECT MAX(Id) FROM EmployeePositionsHistory WHERE EmployeeId = e.Id)
          AND h.DepartmentId = @depId
          AND h.Action != 3
        ORDER BY e.Fio";

            DataTable dtBase = Query(sql, new SQLiteParameter("@depId", departmentId));

            // Создаем результирующую таблицу
            DataTable dtResult = new DataTable();
            dtResult.Columns.Add("EmployeeId", typeof(long));
            dtResult.Columns.Add("FullName", typeof(string));
            dtResult.Columns.Add("Position", typeof(string));
            // Колонку "WorkName" убрали отсюда

            int daysInMonth = DateTime.DaysInMonth(year, month);
            for (int i = 1; i <= daysInMonth; i++)
                dtResult.Columns.Add($"Day{i}", typeof(string)); // Для часов используем строку или double

            dtResult.Columns.Add("Total", typeof(double));

            // Заполняем людьми
            foreach (DataRow row in dtBase.Rows)
            {
                DataRow newRow = dtResult.NewRow();
                newRow["EmployeeId"] = row["EmployeeId"];
                newRow["FullName"] = row["FullName"];
                newRow["Position"] = row["PositionName"];
                newRow["Total"] = 0;
                dtResult.Rows.Add(newRow);
            }

            return dtResult;
        }
        public Dictionary<long, int> GetDepartmentsEmployeeCounts(int year, int month)
        {
            // SQLite понимает формат ISO (гггг-мм-дд). Сделаем дату последнего дня месяца.
            // Это гарантирует правильное строковое сравнение в SQL.
            string lastDayStr = new DateTime(year, month, DateTime.DaysInMonth(year, month))
                                .ToString("yyyy-MM-dd");

            string sql = @"
        SELECT h.DepartmentId, COUNT(*) as EmpCount
        FROM EmployeePositionsHistory h
        WHERE h.Id IN (
            SELECT MAX(Id) 
            FROM EmployeePositionsHistory 
            WHERE StartDate <= @lastDate -- Используем StartDate из твоей схемы
            GROUP BY EmployeeId
        )
        AND h.Action != 3 -- Не уволен
        GROUP BY h.DepartmentId";

            // Передаем дату в формате YYYY-MM-DD
            var dt = Query(sql, new SQLiteParameter("@lastDate", lastDayStr));

            var dict = new Dictionary<long, int>();
            foreach (DataRow r in dt.Rows)
            {
                dict[Convert.ToInt64(r["DepartmentId"])] = Convert.ToInt32(r["EmpCount"]);
            }
            return dict;
        }
        public DataTable GetDepartmentsTable()
        {
            return Departments_List(); // Используем уже существующий метод
        }
        // Метод для получения списка видов работ
        public DataTable GetWorkTypesTable()
        {
            // Название таблицы проверь в своей БД, обычно это WorkTypes или ListOfWork
            string sql = "SELECT Id, Name FROM WorkTypes ORDER BY Name";
            return Query(sql); // Или как там у тебя называется метод выполнения SQL
        }
        public DataTable GetTimeSheetData(long departmentId, int year, int month)
        {
            // 1. Получаем список сотрудников подразделения на указанную дату
            // Берем только тех, у кого последняя запись в истории — это наше подразделение и они не уволены (Action != 3)
            string sql = @"
        SELECT 
            e.Id as EmployeeId, 
            e.Fio as FullName, 
            p.Name as PositionName
        FROM Employees e
        JOIN EmployeePositionsHistory h ON e.Id = h.EmployeeId
        JOIN Positions p ON h.PositionId = p.Id
        WHERE h.Id = (SELECT MAX(Id) FROM EmployeePositionsHistory WHERE EmployeeId = e.Id)
          AND h.DepartmentId = @depId
          AND h.Action != 3
        ORDER BY e.Fio";

            DataTable dtBase = Query(sql, new SQLiteParameter("@depId", departmentId));

            // 2. Создаем структуру таблицы для отображения в DataGrid
            DataTable dtResult = new DataTable();
            dtResult.Columns.Add("EmployeeId", typeof(long));
            dtResult.Columns.Add("FullName", typeof(string));
            dtResult.Columns.Add("RowType", typeof(string)); // 'Employee' или 'Work'

            int daysInMonth = DateTime.DaysInMonth(year, month);
            for (int i = 1; i <= 31; i++)
                dtResult.Columns.Add($"Day{i}", typeof(string));

            dtResult.Columns.Add("Total", typeof(string));

            // 3. Заполняем результат строками сотрудников
            foreach (DataRow row in dtBase.Rows)
            {
                DataRow empRow = dtResult.NewRow();
                empRow["EmployeeId"] = row["EmployeeId"];
                empRow["FullName"] = row["FullName"];
                empRow["RowType"] = "Employee";

                // Тут можно будет потом добавить загрузку уже сохраненных часов из БД
                // Но пока создаем пустые строки
                dtResult.Rows.Add(empRow);
            }

            return dtResult;
        }
        public DataTable GetTimeSheetDataByGroup(string idsString, int year, int month)
        {
            // Вместо @depId используем подстановку строки в условие IN
            // Важно: так как idsString мы генерируем сами из списка long, это безопасно
            string sql = $@"
        SELECT 
            e.Id as EmployeeId, 
            e.Fio as FullName, 
            p.Name as PositionName
        FROM Employees e
        JOIN EmployeePositionsHistory h ON e.Id = h.EmployeeId
        JOIN Positions p ON h.PositionId = p.Id
        WHERE h.Id = (SELECT MAX(Id) FROM EmployeePositionsHistory WHERE EmployeeId = e.Id)
          AND h.DepartmentId IN ({idsString})
          AND h.Action != 3
        ORDER BY e.Fio";

            DataTable dtBase = Query(sql);

            // Дальше логика создания dtResult остается такой же, как была раньше
            DataTable dtResult = new DataTable();
            dtResult.Columns.Add("EmployeeId", typeof(long));
            dtResult.Columns.Add("FullName", typeof(string));
            dtResult.Columns.Add("RowType", typeof(string));
            dtResult.Columns.Add("EPH_Id", typeof(long));    // EmployeePositionsHistoryId
            dtResult.Columns.Add("WorkId", typeof(long));    // Id из ListOfWork
            dtResult.Columns.Add("ProgramId", typeof(long)); // Id из Programs
            dtResult.Columns.Add("TimesheetId", typeof(long)); // Ссылка на заголовок табеля

            for (int i = 1; i <= 31; i++) dtResult.Columns.Add($"Day{i}", typeof(string));
            dtResult.Columns.Add("Total", typeof(string));

            foreach (DataRow row in dtBase.Rows)
            {
                DataRow empRow = dtResult.NewRow();
                empRow["EmployeeId"] = row["EmployeeId"];
                empRow["FullName"] = row["FullName"];
                empRow["RowType"] = "Employee";
                dtResult.Rows.Add(empRow);
            }

            return dtResult;
        }
        public DataTable GetFullTimesheetData(string depIds, int year, int month, long programId)
        {
            string lastDayStr = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            // ВНИМАНИЕ: Здесь изменен порядок JOIN! 
            // Сначала цепляем Timesheet (голову), а к ней уже TimesheetEntry (часы)
            string sql = $@"
        SELECT 
            h.Id as EPH_Id,
            e.Fio,
            ts.Id as TimesheetId,
            te.WorkId,
            lw.Name as WorkName,
            lw.SpecialCode as WorkCode,
            te.WorkDate,
            te.Minutes
        FROM EmployeePositionsHistory h
        JOIN Employees e ON h.EmployeeId = e.Id
        -- 1. Находим заголовок табеля для этого чела по этой программе в этот период
        LEFT JOIN Timesheet ts ON h.Id = ts.EmployeePositionsHistoryId 
            AND ts.ProgramId = @progId 
            AND ts.Year = @year 
            AND ts.Month = @month
        -- 2. Цепляем детальные записи часов по ID табеля
        LEFT JOIN TimesheetEntry te ON ts.Id = te.TimesheetId
        -- 3. Названия работ
        LEFT JOIN ListOfWork lw ON te.WorkId = lw.Id
        WHERE h.Id IN (
            SELECT MAX(Id) FROM EmployeePositionsHistory 
            WHERE StartDate <= @lastDayMonth 
            GROUP BY EmployeeId
        )
        AND h.DepartmentId IN ({depIds})
        AND h.Action != 3
        ORDER BY e.Fio, lw.Name";

            DataTable dtRaw = Query(sql,
                new SQLiteParameter("@lastDayMonth", lastDayStr),
                new SQLiteParameter("@progId", programId),
                new SQLiteParameter("@year", year),
                new SQLiteParameter("@month", month));

            // Создаем структуру с колонкой TimesheetId
            DataTable dtResult = new DataTable();
            dtResult.Columns.Add("EPH_Id", typeof(long));
            dtResult.Columns.Add("TimesheetId", typeof(long)); // Наша скрытая колонка
            dtResult.Columns.Add("FullName", typeof(string));
            dtResult.Columns.Add("RowType", typeof(string));
            dtResult.Columns.Add("WorkId", typeof(long));
            dtResult.Columns.Add("ProgramId", typeof(long));
            dtResult.Columns.Add("WorkCode", typeof(string));
            for (int i = 1; i <= 31; i++) dtResult.Columns.Add($"Day{i}", typeof(string));
            dtResult.Columns.Add("Total", typeof(string));

            var groups = dtRaw.AsEnumerable().GroupBy(r => r.Field<long>("EPH_Id"));

            foreach (var empGroup in groups)
            {
                var first = empGroup.First();

                // СТРОКА СОТРУДНИКА
                DataRow empRow = dtResult.NewRow();
                empRow["EPH_Id"] = empGroup.Key;
                // Если табель в БД есть, записываем его Id, иначе будет DBNull
                empRow["TimesheetId"] = first["TimesheetId"];
                empRow["FullName"] = first.Field<string>("Fio");
                empRow["WorkCode"] = ""; // У сотрудника нет кода работы
                empRow["RowType"] = "Employee";
                dtResult.Rows.Add(empRow);

                var workGroups = empGroup.Where(r => r["WorkId"] != DBNull.Value)
                                         .GroupBy(r => Convert.ToInt64(r["WorkId"]));

                foreach (var workGroup in workGroups)
                {
                    DataRow workRow = dtResult.NewRow();
                    workRow["EPH_Id"] = empGroup.Key;
                    workRow["TimesheetId"] = first["TimesheetId"]; // В строке работы тоже храним ID табеля
                    workRow["WorkId"] = workGroup.Key;
                    workRow["ProgramId"] = programId;
                    workRow["RowType"] = "Work";
                    workRow["FullName"] = "   • " + workGroup.First().Field<string>("WorkName");
                    workRow["WorkCode"] = workGroup.First().Field<string>("WorkCode");

                    foreach (var entry in workGroup)
                    {
                        if (entry["WorkDate"] != DBNull.Value)
                        {
                            DateTime d = DateTime.Parse(entry["WorkDate"].ToString());
                            int day = d.Day;
                            int mins = Convert.ToInt32(entry["Minutes"]);
                            int hrs = mins / 60;
                            int mns = mins % 60;
                            workRow[$"Day{day}"] = $"{hrs:D2}:{mns:D2}";
                        }
                    }
                    dtResult.Rows.Add(workRow);
                }
            }
            return dtResult;
        }
        // В DbService.cs измени void на long
        public long SaveTimesheetEntry(long ephId, long workId, long progId, string dateStr, int minutes, int year, int month)
        {
            long tsId = GetOrCreateTimesheet(ephId, progId, year, month);

            if (minutes <= 0)
            {
                Execute("DELETE FROM TimesheetEntry WHERE TimesheetId=@tsId AND WorkId=@wid AND WorkDate=@date",
                    new SQLiteParameter("@tsId", tsId), new SQLiteParameter("@wid", workId), new SQLiteParameter("@date", dateStr));
            }
            else
            {
                Execute("INSERT OR REPLACE INTO TimesheetEntry (TimesheetId, WorkId, WorkDate, Minutes) VALUES (@tsId, @wid, @date, @mins)",
                    new SQLiteParameter("@tsId", tsId), new SQLiteParameter("@wid", workId),
                    new SQLiteParameter("@date", dateStr), new SQLiteParameter("@mins", minutes));
            }
            return tsId; // Возвращаем ID табеля
        }
        public long GetOrCreateTimesheet(long ephId, long progId, int year, int month)
        {
            object id = Scalar(@"SELECT Id FROM Timesheet 
                         WHERE EmployeePositionsHistoryId=@e AND ProgramId=@p AND Year=@y AND Month=@m",
                new SQLiteParameter("@e", ephId), new SQLiteParameter("@p", progId),
                new SQLiteParameter("@y", year), new SQLiteParameter("@m", month));

            if (id != null) return Convert.ToInt64(id);

            return (long)Scalar(@"INSERT INTO Timesheet (EmployeePositionsHistoryId, ProgramId, Year, Month, CreatedAt) 
                          VALUES (@e, @p, @y, @m, @c); SELECT last_insert_rowid();",
                new SQLiteParameter("@e", ephId), new SQLiteParameter("@p", progId),
                new SQLiteParameter("@y", year), new SQLiteParameter("@m", month),
                new SQLiteParameter("@c", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        }

        public void SaveTimesheetEntriesBulk(List<TimesheetEntryDto> entries, int year, int month)
        {
            if (entries == null || entries.Count == 0) return;

            InTransaction((conn, tx) =>
            {
                var tsCache = new Dictionary<string, long>();
                using (var cmdSelect = conn.CreateCommand())
                using (var cmdInsertTs = conn.CreateCommand())
                using (var cmdDelete = conn.CreateCommand())
                using (var cmdInsertEntry = conn.CreateCommand())
                {
                    cmdSelect.Transaction = tx; cmdInsertTs.Transaction = tx;
                    cmdDelete.Transaction = tx; cmdInsertEntry.Transaction = tx;

                    cmdSelect.CommandText = "SELECT Id FROM Timesheet WHERE EmployeePositionsHistoryId=@e AND ProgramId=@p AND Year=@y AND Month=@m";
                    cmdInsertTs.CommandText = "INSERT INTO Timesheet (EmployeePositionsHistoryId, ProgramId, Year, Month, CreatedAt) VALUES (@e, @p, @y, @m, @c); SELECT last_insert_rowid();";
                    cmdDelete.CommandText = "DELETE FROM TimesheetEntry WHERE TimesheetId=@tsId AND WorkId=@wid AND WorkDate=@date";
                    cmdInsertEntry.CommandText = "INSERT OR REPLACE INTO TimesheetEntry (TimesheetId, WorkId, WorkDate, Minutes) VALUES (@tsId, @wid, @date, @mins)";

                    foreach (var item in entries)
                    {
                        string key = $"{item.EphId}_{item.ProgramId}";
                        if (!tsCache.TryGetValue(key, out long tsId))
                        {
                            cmdSelect.Parameters.Clear();
                            cmdSelect.Parameters.AddWithValue("@e", item.EphId);
                            cmdSelect.Parameters.AddWithValue("@p", item.ProgramId);
                            cmdSelect.Parameters.AddWithValue("@y", year);
                            cmdSelect.Parameters.AddWithValue("@m", month);
                            object dbId = cmdSelect.ExecuteScalar();

                            if (dbId != null) tsId = Convert.ToInt64(dbId);
                            else
                            {
                                cmdInsertTs.Parameters.Clear();
                                cmdInsertTs.Parameters.AddWithValue("@e", item.EphId);
                                cmdInsertTs.Parameters.AddWithValue("@p", item.ProgramId);
                                cmdInsertTs.Parameters.AddWithValue("@y", year);
                                cmdInsertTs.Parameters.AddWithValue("@m", month);
                                cmdInsertTs.Parameters.AddWithValue("@c", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                tsId = (long)cmdInsertTs.ExecuteScalar();
                            }
                            tsCache[key] = tsId;
                        }

                        if (item.Minutes <= 0)
                        {
                            cmdDelete.Parameters.Clear();
                            cmdDelete.Parameters.AddWithValue("@tsId", tsId);
                            cmdDelete.Parameters.AddWithValue("@wid", item.WorkId);
                            cmdDelete.Parameters.AddWithValue("@date", item.DateStr);
                            cmdDelete.ExecuteNonQuery();
                        }
                        else
                        {
                            cmdInsertEntry.Parameters.Clear();
                            cmdInsertEntry.Parameters.AddWithValue("@tsId", tsId);
                            cmdInsertEntry.Parameters.AddWithValue("@wid", item.WorkId);
                            cmdInsertEntry.Parameters.AddWithValue("@date", item.DateStr);
                            cmdInsertEntry.Parameters.AddWithValue("@mins", item.Minutes);
                            cmdInsertEntry.ExecuteNonQuery();
                        }
                    }
                }
            });
        }
        public DataTable Programs_List_Actual(int year, int month)
        {
            // Начало и конец выбранного месяца в формате ISO для сравнения строк
            DateTime monthStart = new DateTime(year, month, 1);
            DateTime monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            string startStr = monthStart.ToString("yyyy-MM-dd");
            string endStr = monthEnd.ToString("yyyy-MM-dd");

            // Программа актуальна, если:
            // 1. Она началась ДО конца выбранного месяца
            // 2. Она НЕ закончилась (NULL) ИЛИ закончилась ПОСЛЕ начала выбранного месяца
            string sql = @"
        SELECT Id, Name, ShortName, DateStart, DateEnd 
        FROM Programs 
        WHERE DateStart <= @endStr 
          AND (DateEnd IS NULL OR DateEnd >= @startStr)
        ORDER BY Name;";

            return Query(sql,
                new SQLiteParameter("@startStr", startStr),
                new SQLiteParameter("@endStr", endStr));
        }

        public void Work_Timesheet_Delete(long tsId, long workId)
        {
            Execute("DELETE FROM TimesheetEntry WHERE TimesheetId=@tsId AND WorkId=@wid",
                new SQLiteParameter("@tsId", tsId),
                new SQLiteParameter("@wid", workId));
        }

        public void Work_Timesheet_Edit(long tsId, long oldWorkId, long newWorkId)
        {
            try
            {
                Execute("UPDATE TimesheetEntry SET WorkId=@newWid WHERE TimesheetId=@tsId AND WorkId=@oldWid",
                    new SQLiteParameter("@newWid", newWorkId),
                    new SQLiteParameter("@tsId", tsId),
                    new SQLiteParameter("@oldWid", oldWorkId));
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                throw new System.Exception("Такой вид работы уже добавлен этому сотруднику!");
            }
        }
        public ImportResult ImportTimesheetDataFlat(DataTable entriesTable, int year, int month)
        {
            var result = new ImportResult();
            if (entriesTable == null || entriesTable.Rows.Count == 0) return result;

            string lastDayStr = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            InTransaction((conn, tx) =>
            {
                var tsCache = new Dictionary<string, long>(); // Cache key: "EphId_ProgId" -> TsId

                using (var cmdFindEmp = conn.CreateCommand())
                using (var cmdFindProg = conn.CreateCommand())
                using (var cmdFindWorkCode = conn.CreateCommand())
                using (var cmdFindWorkName = conn.CreateCommand())
                using (var cmdGetTsId = conn.CreateCommand())
                using (var cmdInsertTs = conn.CreateCommand())
                using (var cmdUpsertEntry = conn.CreateCommand())
                {
                    cmdFindEmp.Transaction = tx; cmdFindProg.Transaction = tx; cmdFindWorkCode.Transaction = tx;
                    cmdFindWorkName.Transaction = tx; cmdGetTsId.Transaction = tx; cmdInsertTs.Transaction = tx;
                    cmdUpsertEntry.Transaction = tx;

                    // SQL Инициализация
                    cmdFindEmp.CommandText = "SELECT h.Id FROM EmployeePositionsHistory h JOIN Employees e ON h.EmployeeId = e.Id WHERE e.Fio = @fio AND h.StartDate <= @lastDay ORDER BY h.Id DESC LIMIT 1";
                    cmdFindProg.CommandText = "SELECT Id FROM Programs WHERE Name = @n OR ShortName = @n LIMIT 1";
                    cmdFindWorkCode.CommandText = "SELECT Id FROM ListOfWork WHERE SpecialCode = @c LIMIT 1";
                    cmdFindWorkName.CommandText = "SELECT Id FROM ListOfWork WHERE Name = @n LIMIT 1";
                    
                    cmdGetTsId.CommandText = "SELECT Id FROM Timesheet WHERE EmployeePositionsHistoryId=@e AND ProgramId=@p AND Year=@y AND Month=@m";
                    cmdInsertTs.CommandText = "INSERT INTO Timesheet (EmployeePositionsHistoryId, ProgramId, Year, Month, CreatedAt) VALUES (@e, @p, @y, @m, @c); SELECT last_insert_rowid();";
                    cmdUpsertEntry.CommandText = "INSERT OR REPLACE INTO TimesheetEntry (TimesheetId, WorkId, WorkDate, Minutes) VALUES (@tsId, @wid, @date, @mins)";

                    foreach (DataRow row in entriesTable.Rows)
                    {
                        string empFio = row["EmployeeFio"]?.ToString()?.Trim();
                        string progName = row["ProgramName"]?.ToString()?.Trim();
                        string workCode = entriesTable.Columns.Contains("WorkCode") ? row["WorkCode"]?.ToString()?.Trim() : "";
                        string workName = entriesTable.Columns.Contains("WorkName") ? row["WorkName"]?.ToString()?.Trim() : "";
                        string dateStr = row["WorkDate"]?.ToString();
                        int minutes = row["Minutes"] != DBNull.Value ? Convert.ToInt32(row["Minutes"]) : 0;

                        if (string.IsNullOrWhiteSpace(empFio) || minutes <= 0) continue;

                        // 1. Поиск Сотрудника
                        cmdFindEmp.Parameters.Clear();
                        cmdFindEmp.Parameters.AddWithValue("@fio", empFio);
                        cmdFindEmp.Parameters.AddWithValue("@lastDay", lastDayStr);
                        object objEph = cmdFindEmp.ExecuteScalar();
                        if (objEph == null) { result.MissingEmployees.Add(empFio); continue; }
                        long ephId = Convert.ToInt64(objEph);

                        // 2. Поиск Программы
                        if (string.IsNullOrWhiteSpace(progName)) { result.MissingPrograms.Add("Не указана"); continue; }
                        cmdFindProg.Parameters.Clear();
                        cmdFindProg.Parameters.AddWithValue("@n", progName);
                        object objProg = cmdFindProg.ExecuteScalar();
                        if (objProg == null) { result.MissingPrograms.Add(progName); continue; }
                        long progId = Convert.ToInt64(objProg);

                        // 3. Поиск Работы
                        long? workId = null;
                        if (!string.IsNullOrWhiteSpace(workCode))
                        {
                            cmdFindWorkCode.Parameters.Clear();
                            cmdFindWorkCode.Parameters.AddWithValue("@c", workCode);
                            object objW = cmdFindWorkCode.ExecuteScalar();
                            if (objW != null) workId = Convert.ToInt64(objW);
                        }
                        if (workId == null && !string.IsNullOrWhiteSpace(workName))
                        {
                            cmdFindWorkName.Parameters.Clear();
                            cmdFindWorkName.Parameters.AddWithValue("@n", workName);
                            object objW = cmdFindWorkName.ExecuteScalar();
                            if (objW != null) workId = Convert.ToInt64(objW);
                        }

                        if (workId == null)
                        {
                            result.MissingWorks.Add(!string.IsNullOrWhiteSpace(workCode) ? $"[{workCode}] {workName}" : (workName ?? "Без работы"));
                            continue;
                        }

                        // 4. Находим/создаем Таймшит (используем кэш для скорости)
                        string cacheKey = $"{ephId}_{progId}";
                        if (!tsCache.TryGetValue(cacheKey, out long tsId))
                        {
                            cmdGetTsId.Parameters.Clear();
                            cmdGetTsId.Parameters.AddWithValue("@e", ephId);
                            cmdGetTsId.Parameters.AddWithValue("@p", progId);
                            cmdGetTsId.Parameters.AddWithValue("@y", year);
                            cmdGetTsId.Parameters.AddWithValue("@m", month);
                            object dbTsId = cmdGetTsId.ExecuteScalar();

                            if (dbTsId != null) tsId = Convert.ToInt64(dbTsId);
                            else
                            {
                                cmdInsertTs.Parameters.Clear();
                                cmdInsertTs.Parameters.AddWithValue("@e", ephId);
                                cmdInsertTs.Parameters.AddWithValue("@p", progId);
                                cmdInsertTs.Parameters.AddWithValue("@y", year);
                                cmdInsertTs.Parameters.AddWithValue("@m", month);
                                cmdInsertTs.Parameters.AddWithValue("@c", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                tsId = (long)cmdInsertTs.ExecuteScalar();
                            }
                            tsCache[cacheKey] = tsId;
                        }

                        // 5. Пишем запись
                        cmdUpsertEntry.Parameters.Clear();
                        cmdUpsertEntry.Parameters.AddWithValue("@tsId", tsId);
                        cmdUpsertEntry.Parameters.AddWithValue("@wid", workId.Value);
                        cmdUpsertEntry.Parameters.AddWithValue("@date", dateStr);
                        cmdUpsertEntry.Parameters.AddWithValue("@mins", minutes);
                        cmdUpsertEntry.ExecuteNonQuery();

                        result.ImportedCount++;
                    }
                }
            });

            return result;
        }

        #region EmployeeSign
        public DataTable EmployeeSign_Get(long employeeId)
        {
            return Query(@"
                SELECT ImageData, OffsetX, OffsetY, Scale 
                FROM EmployeeSign 
                WHERE EmployeeId = @eid",
                new SQLiteParameter("@eid", employeeId));
        }

        public void EmployeeSign_Save(long employeeId, byte[] imageData, double offsetX, double offsetY, double scale)
        {
            // SQLite 3.24+ поддерживает UPSERT (ON CONFLICT)
            Execute(@"
                INSERT INTO EmployeeSign (EmployeeId, ImageData, OffsetX, OffsetY, Scale)
                VALUES (@eid, @data, @ox, @oy, @s)
                ON CONFLICT(EmployeeId) DO UPDATE SET 
                    ImageData = CASE WHEN excluded.ImageData IS NOT NULL THEN excluded.ImageData ELSE EmployeeSign.ImageData END,
                    OffsetX = excluded.OffsetX,
                    OffsetY = excluded.OffsetY,
                    Scale = excluded.Scale;",
                new SQLiteParameter("@eid", employeeId),
                new SQLiteParameter("@data", imageData ?? (object)DBNull.Value),
                new SQLiteParameter("@ox", offsetX),
                new SQLiteParameter("@oy", offsetY),
                new SQLiteParameter("@s", scale));
            
            LogService.Log($"ПОДПИСЬ СОХРАНЕНА (ID Сотрудника: {employeeId})");
        }

        public void EmployeeSign_Delete(long employeeId)
        {
            Execute("DELETE FROM EmployeeSign WHERE EmployeeId = @eid",
                new SQLiteParameter("@eid", employeeId));
            LogService.Log($"ПОДПИСЬ УДАЛЕНА (ID Сотрудника: {employeeId})");
        }
        #endregion
    }

    public class ImportResult
    {
        public int ImportedCount { get; set; }
        public HashSet<string> MissingEmployees { get; set; } = new HashSet<string>();
        public HashSet<string> MissingPrograms { get; set; } = new HashSet<string>();
        public HashSet<string> MissingWorks { get; set; } = new HashSet<string>();
    }

    public class TimesheetEntryDto
    {
        public long EphId { get; set; }
        public long WorkId { get; set; }
        public long ProgramId { get; set; }
        public string DateStr { get; set; }
        public int Minutes { get; set; }
    }
}