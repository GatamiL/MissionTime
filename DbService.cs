using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MissionTime.DbSchema;

namespace MissionTime
{
    public sealed class DbService
    {
        #region Состояние и свойства
        public string DbPath { get; private set; }
        public string ConnectionString { get; private set; }
        public bool IsConnected => !string.IsNullOrWhiteSpace(ConnectionString);
        #endregion
        #region Инициализация и подключение
        public void Connect(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is empty.");

            if (!DbSchema.Validate(dbPath, out var err))
                throw new InvalidOperationException("DB schema validation failed: " + err);

            DbPath = dbPath;
            ConnectionString = DbSchema.GetConnectionString(dbPath);

            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1;";
                cmd.ExecuteScalar();
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
                return (long)cmd.ExecuteScalar();
            }
        }
        public int Position_Update(long id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("name empty");

            return Execute(
                "UPDATE Positions SET Name=@n WHERE Id=@id;",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@id", id)
            );
        }
        public int Position_Delete(long id)
        {
            return Execute(
                "DELETE FROM Positions WHERE Id=@id;",
                new SQLiteParameter("@id", id)
            );
        }
        public (int DepartmentId, int PositionId)? Employee_GetCurrentDeptPos(long employeeId)
        {
            var dt = Query(@"
        SELECT DepartmentId, PositionId
        FROM EmployeePositionsHistory
        WHERE EmployeeId=@e AND EndDate IS NULL
        ORDER BY StartDate DESC, Id DESC
        LIMIT 1;",
                new SQLiteParameter("@e", employeeId));

            if (dt.Rows.Count == 0) return null;

            return (
                Convert.ToInt32(dt.Rows[0]["DepartmentId"]),
                Convert.ToInt32(dt.Rows[0]["PositionId"])
            );
        }
        #endregion
        #region ListOfWork
        public System.Data.DataTable LOW_List()
        {
            return Query(@"
                            SELECT
                              Id,
                              Name,
                              SpecialCode
                            FROM ListOfWork
                            ORDER BY Name;");
        }
        public System.Data.DataTable ListOfWork_AllForCombo()
        {
            return Query(@"
SELECT
  Id,
  CASE
    WHEN SpecialCode IS NULL OR TRIM(SpecialCode) = '' THEN Name
    ELSE SpecialCode || ' — ' || Name
  END AS DisplayName
FROM ListOfWork
ORDER BY Name;");
        }
        public DataTable ListOfWork_ByIds(IEnumerable<long> ids)
        {
            var list = ids?.Distinct().ToList() ?? new List<long>();
            if (list.Count == 0)
            {
                var empty = new DataTable();
                empty.Columns.Add("WorkId", typeof(long));
                empty.Columns.Add("WorkName", typeof(string));
                return empty;
            }

            // параметры @id0,@id1,...
            var prms = new List<SQLiteParameter>();
            var inParts = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                string p = "@id" + i;
                inParts.Add(p);
                prms.Add(new SQLiteParameter(p, list[i]));
            }

            return Query($@"
SELECT Id AS WorkId, Name AS WorkName
FROM ListOfWork
WHERE Id IN ({string.Join(",", inParts)})
ORDER BY Name;", prms.ToArray());
        }
        public long LOW_Create(long? parentId, string name, string specialCode)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name empty");
            if (specialCode == null) specialCode = "";

            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO ListOfWork(ParentId, Name, SpecialCode)
                    VALUES(@pid, @name, @code);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@pid", parentId.HasValue ? (object)parentId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                cmd.Parameters.AddWithValue("@code", specialCode.Trim());
                return (long)cmd.ExecuteScalar();
            }
        }
        public int LOW_Update(long id, long? parentId, string name, string specialCode)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name empty");
            if (specialCode == null) specialCode = "";

            return Execute(@"
                UPDATE ListOfWork
                SET ParentId=@pid, Name=@name, SpecialCode=@code
                WHERE Id=@id;",
                new System.Data.SQLite.SQLiteParameter("@pid", parentId.HasValue ? (object)parentId.Value : DBNull.Value),
                new System.Data.SQLite.SQLiteParameter("@name", name.Trim()),
                new System.Data.SQLite.SQLiteParameter("@code", specialCode.Trim()),
                new System.Data.SQLite.SQLiteParameter("@id", id)
            );
        }
        public int LOW_Delete(long id)
        {
            return Execute("DELETE FROM ListOfWork WHERE Id=@id;",
                new System.Data.SQLite.SQLiteParameter("@id", id));
        }
        #endregion
        #region Programs
        public DataTable Programs_List()
        {
            return Query(@"
                SELECT Id, Name, ShortName, DateStart, DateEnd
                FROM Programs
                ORDER BY DateStart DESC, Name;");
        }
        public DataTable Programs_ListForMonth(int year, int month)
        {
            var periodStart = new DateTime(year, month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            return Query(@"
                SELECT Id, ShortName
                FROM Programs
                WHERE Date(DateStart) <= Date(@pe)
                  AND (DateEnd IS NULL OR Date(DateEnd) >= Date(@ps))
                ORDER BY ShortName;",
                new SQLiteParameter("@ps", periodStart.ToString("yyyy-MM-dd")),
                new SQLiteParameter("@pe", periodEnd.ToString("yyyy-MM-dd"))
            );
        }
        public (DateTime StartDate, DateTime EndDate)? Programs_GetById(int id)
        {
            var dt = Query(@"
        SELECT DateStart, DateEnd
        FROM Programs
        WHERE Id=@id
        LIMIT 1;",
                new SQLiteParameter("@id", id));

            if (dt.Rows.Count == 0)
                return null;

            DateTime start = DateTime.Parse(Convert.ToString(dt.Rows[0]["DateStart"]));

            // DateEnd может быть NULL
            DateTime end;
            if (dt.Rows[0]["DateEnd"] == DBNull.Value || dt.Rows[0]["DateEnd"] == null ||
                string.IsNullOrWhiteSpace(Convert.ToString(dt.Rows[0]["DateEnd"])))
            {
                // если конец не задан — считаем, что программа до конца выбранного месяца/условно "бесконечно"
                end = DateTime.MaxValue.Date;
            }
            else
            {
                end = DateTime.Parse(Convert.ToString(dt.Rows[0]["DateEnd"]));
            }

            return (start, end);
        }
        public long Program_Create(string name, string shortName, DateTime dateStart, DateTime? dateEnd)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name empty");
            if (string.IsNullOrWhiteSpace(shortName)) throw new ArgumentException("shortName empty");
            if (dateEnd.HasValue && dateEnd.Value.Date < dateStart.Date)
                throw new ArgumentException("dateEnd < dateStart");

            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Programs(Name, ShortName, DateStart, DateEnd)
                    VALUES(@n, @sn, @ds, @de);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@n", name.Trim());
                cmd.Parameters.AddWithValue("@sn", shortName.Trim());
                cmd.Parameters.AddWithValue("@ds", dateStart.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@de", dateEnd.HasValue ? (object)dateEnd.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                return (long)cmd.ExecuteScalar();
            }
        }
        public int Program_Update(long id, string name, string shortName, DateTime dateStart, DateTime? dateEnd)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name empty");
            if (string.IsNullOrWhiteSpace(shortName)) throw new ArgumentException("shortName empty");
            if (dateEnd.HasValue && dateEnd.Value.Date < dateStart.Date)
                throw new ArgumentException("dateEnd < dateStart");

            return Execute(@"
                UPDATE Programs
                SET Name=@n, ShortName=@sn, DateStart=@ds, DateEnd=@de
                WHERE Id=@id;",
                new SQLiteParameter("@n", name.Trim()),
                new SQLiteParameter("@sn", shortName.Trim()),
                new SQLiteParameter("@ds", dateStart.ToString("yyyy-MM-dd")),
                new SQLiteParameter("@de", dateEnd.HasValue ? (object)dateEnd.Value.ToString("yyyy-MM-dd") : DBNull.Value),
                new SQLiteParameter("@id", id)
            );
        }
        public int Program_Delete(long id)
        {
            return Execute("DELETE FROM Programs WHERE Id=@id;", new SQLiteParameter("@id", id));
        }
        #endregion
        #region Departments
        public DataTable Departments_List()
        {
            return Query(
                @"SELECT Id, ParentId, Name, Level
          FROM Departments
          ORDER BY Level, SortOrder, Name;");
        }
        public DataTable Departments_ListOnlyLevel3()
        {
            return Query(@"
        SELECT 
            Id,
            Name AS DisplayName
        FROM Departments
        WHERE Level = 3
        ORDER BY Name;
    ");
        }
        public DataTable Departments_ListForComboAnyLevel()
        {
            return Query(@"
                SELECT
                  Id,
                  (CASE Level
                     WHEN 1 THEN 'Центр: '
                     WHEN 2 THEN 'Комплекс: '
                     WHEN 3 THEN 'Отдел: '
                     WHEN 4 THEN 'Группа: '
                     ELSE ''
                   END) || Name AS Name
                FROM Departments
                ORDER BY Level, Name;");
        }
        public DataTable Departments_ListHierarchicalForCombo_WithEmployeeCountsForMonth(int year, int month)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            var dt = Query(@"
WITH RECURSIVE
anc(ancestorId, deptId) AS (
    SELECT Id, Id
    FROM Departments
    UNION ALL
    SELECT d.ParentId, anc.deptId
    FROM anc
    JOIN Departments d ON d.Id = anc.ancestorId
    WHERE d.ParentId IS NOT NULL
),
empInMonth AS (
    SELECT DISTINCT EmployeeId, DepartmentId
    FROM EmployeePositionsHistory
    WHERE date(StartDate) <= date(@monthEnd)
      AND (EndDate IS NULL OR date(EndDate) >= date(@monthStart))
),
cnt AS (
    SELECT anc.ancestorId AS DeptId,
           COUNT(DISTINCT empInMonth.EmployeeId) AS EmpCount
    FROM empInMonth
    JOIN anc ON anc.deptId = empInMonth.DepartmentId
    GROUP BY anc.ancestorId
),
tree AS (
    -- корни
    SELECT
        d.Id, d.ParentId, d.Level, d.SortOrder, d.Name,
        COALESCE(c.EmpCount, 0) AS EmpCount,
        (printf('%04d', d.SortOrder) || '|' || d.Name || '|' || printf('%010d', d.Id)) AS SortPath
    FROM Departments d
    LEFT JOIN cnt c ON c.DeptId = d.Id
    WHERE d.ParentId IS NULL

    UNION ALL

    -- дети
    SELECT
        d.Id, d.ParentId, d.Level, d.SortOrder, d.Name,
        COALESCE(c.EmpCount, 0) AS EmpCount,
        (tree.SortPath || '/' || printf('%04d', d.SortOrder) || '|' || d.Name || '|' || printf('%010d', d.Id)) AS SortPath
    FROM Departments d
    JOIN tree ON tree.Id = d.ParentId
    LEFT JOIN cnt c ON c.DeptId = d.Id
)
SELECT Id, Level, Name, EmpCount
FROM tree
WHERE EmpCount > 0
ORDER BY SortPath;",
                new SQLiteParameter("@monthStart", monthStart),
                new SQLiteParameter("@monthEnd", monthEnd)
            );

            // Формируем DisplayName с отступом + (count)
            var outDt = new DataTable();
            outDt.Columns.Add("Id", typeof(long));
            outDt.Columns.Add("DisplayName", typeof(string));

            foreach (DataRow r in dt.Rows)
            {
                long id = Convert.ToInt64(r["Id"]);
                int level = Convert.ToInt32(r["Level"]);
                string name = Convert.ToString(r["Name"]);
                int empCount = Convert.ToInt32(r["EmpCount"]);

                string indent = new string(' ', Math.Max(0, (level - 1) * 2));
                string display = $"{indent}{name} ({empCount})";

                var nr = outDt.NewRow();
                nr["Id"] = id;
                nr["DisplayName"] = display;
                outDt.Rows.Add(nr);
            }

            return outDt;
        }
        public long Department_Create(string name, DepartmentLevel level, long? parentId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is empty");

            if (level == DepartmentLevel.Center && parentId != null)
                throw new InvalidOperationException("Центр не может иметь родителя");

            if (level != DepartmentLevel.Center && parentId == null)
                throw new InvalidOperationException("Уровень требует родителя");

            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    @"INSERT INTO Departments (ParentId, Name, Level, SortOrder)
              VALUES (@parent, @name, @level, 0);
              SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@parent", parentId.HasValue ? (object)parentId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                cmd.Parameters.AddWithValue("@level", (int)level);

                return (long)cmd.ExecuteScalar();
            }
        }
        public int Department_Update(long id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("newName empty");

            return Execute(
                "UPDATE Departments SET Name=@name WHERE Id=@id;",
                new SQLiteParameter("@name", newName.Trim()),
                new SQLiteParameter("@id", id)
            );
        }
        public int Department_Delete(long id)
        {
            return Execute(
                "DELETE FROM Departments WHERE Id=@id;",
                new SQLiteParameter("@id", id)
            );
        }
        public long EPH_GetDepartmentId(long ephId)
        {
            var obj = Scalar("SELECT DepartmentId FROM EmployeePositionsHistory WHERE Id=@id;",
                new SQLiteParameter("@id", ephId));

            if (obj == null || obj == DBNull.Value)
                throw new InvalidOperationException("EPH not found: " + ephId);

            return Convert.ToInt64(obj);
        }
        #endregion
        #region Employees
        public DataTable Employee_List(bool showFired)
        {
            return Query(@"
                SELECT
                    e.Id,
                    e.Fio,
                    p.Name AS PositionName,
                    d.Name AS DepartmentName
                FROM Employees e
                LEFT JOIN EmployeePositionsHistory eph
                    ON eph.EmployeeId = e.Id
                    AND eph.EndDate IS NULL
                LEFT JOIN Positions p ON p.Id = eph.PositionId
                LEFT JOIN Departments d ON d.Id = eph.DepartmentId
                WHERE (@showFired = 1) OR (eph.Id IS NOT NULL)
                ORDER BY e.Fio;",
                new SQLiteParameter("@showFired", showFired ? 1 : 0)
            );
        }
        public DataRow Employee_GetByEphId(long ephId)
        {
            var dt = Query(@"
SELECT
    e.Fio AS Fio,
    d.Name AS DepartmentName
FROM EmployeePositionsHistory h
JOIN Employees e ON e.Id = h.EmployeeId
JOIN Departments d ON d.Id = h.DepartmentId
WHERE h.Id = @id
LIMIT 1;",
                new SQLiteParameter("@id", ephId)
            );

            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }
        public int Employee_CreateAndHire(string fio, int departmentId, int positionId, DateTime startDate, string note = null)
        {
            if (string.IsNullOrWhiteSpace(fio))
                throw new ArgumentException("fio is empty.", nameof(fio));

            if (departmentId <= 0) throw new ArgumentOutOfRangeException(nameof(departmentId));
            if (positionId <= 0) throw new ArgumentOutOfRangeException(nameof(positionId));

            var sd = startDate.Date.ToString("yyyy-MM-dd");

            return InTransactionReturn((conn, tx) =>
            {
                long empId;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO Employees(Fio) VALUES(@fio);
                                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@fio", fio.Trim());
                    empId = (long)cmd.ExecuteScalar();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO EmployeePositionsHistory
                        (EmployeeId, DepartmentId, PositionId, StartDate, EndDate, Action, Note)
                        VALUES
                        (@eid, @did, @pid, @sd, NULL, @act, @note);";
                    cmd.Parameters.AddWithValue("@eid", empId);
                    cmd.Parameters.AddWithValue("@did", departmentId);
                    cmd.Parameters.AddWithValue("@pid", positionId);
                    cmd.Parameters.AddWithValue("@sd", sd);
                    cmd.Parameters.AddWithValue("@act", (int)DbSchema.EmployeeAction.Hire);
                    cmd.Parameters.AddWithValue("@note", (object)note ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }

                return (int)empId;
            });
        }
        public void Employee_UpdateFio(int employeeId, string newFio)
        {
            if (employeeId <= 0) throw new ArgumentOutOfRangeException(nameof(employeeId));
            if (string.IsNullOrWhiteSpace(newFio))
                throw new ArgumentException("newFio is empty.", nameof(newFio));

            var affected = Execute(
                "UPDATE Employees SET Fio=@fio WHERE Id=@id;",
                new SQLiteParameter("@fio", newFio.Trim()),
                new SQLiteParameter("@id", employeeId)
            );

            if (affected == 0)
                throw new InvalidOperationException("Employee not found.");
        }
        public void Employee_Transfer(int employeeId, int newDepartmentId, int newPositionId, DateTime transferDate, string note = null)
        {
            if (employeeId <= 0) throw new ArgumentOutOfRangeException(nameof(employeeId));
            if (newDepartmentId <= 0) throw new ArgumentOutOfRangeException(nameof(newDepartmentId));
            if (newPositionId <= 0) throw new ArgumentOutOfRangeException(nameof(newPositionId));

            var trDate = transferDate.Date;
            var trDateText = trDate.ToString("yyyy-MM-dd");

            InTransaction((conn, tx) =>
            {
                long currentEphId;
                DateTime currentStartDate;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
SELECT Id, StartDate
FROM EmployeePositionsHistory
WHERE EmployeeId=@eid AND EndDate IS NULL
LIMIT 1;";
                    cmd.Parameters.AddWithValue("@eid", employeeId);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            throw new InvalidOperationException("У сотрудника нет активной должности (EndDate IS NULL).");

                        currentEphId = (long)r["Id"];
                        currentStartDate = DateTime.Parse(r["StartDate"].ToString()).Date;
                    }
                }

                // нельзя раньше текущего назначения
                if (trDate < currentStartDate)
                    throw new InvalidOperationException("Дата перевода раньше даты назначения на текущую должность.");

                // важно: чтобы старая должность была "до дня перевода"
                if (trDate == currentStartDate)
                    throw new InvalidOperationException("Перевод в день назначения невозможен. Выберите дату позже (минимум на следующий день).");

                var oldEndDate = trDate.AddDays(-1);
                var oldEndText = oldEndDate.ToString("yyyy-MM-dd");

                // закрываем текущую запись на день раньше
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE EmployeePositionsHistory SET EndDate=@ed, Action=@act, Note=@note WHERE Id=@id;";
                    cmd.Parameters.AddWithValue("@ed", oldEndText);
                    cmd.Parameters.AddWithValue("@act", (int)DbSchema.EmployeeAction.Transfer);
                    cmd.Parameters.AddWithValue("@note", (object)note ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", currentEphId);
                    cmd.ExecuteNonQuery();
                }

                // создаём новую запись с датой перевода
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
INSERT INTO EmployeePositionsHistory
(EmployeeId, DepartmentId, PositionId, StartDate, EndDate, Action, Note)
VALUES
(@eid, @did, @pid, @sd, NULL, @act, @note);";
                    cmd.Parameters.AddWithValue("@eid", employeeId);
                    cmd.Parameters.AddWithValue("@did", newDepartmentId);
                    cmd.Parameters.AddWithValue("@pid", newPositionId);
                    cmd.Parameters.AddWithValue("@sd", trDateText);
                    cmd.Parameters.AddWithValue("@act", (int)DbSchema.EmployeeAction.Transfer);
                    cmd.Parameters.AddWithValue("@note", (object)note ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            });
        }
        public void Employee_Fire(int employeeId, DateTime fireDate, string note = null)
        {
            if (employeeId <= 0) throw new ArgumentOutOfRangeException(nameof(employeeId));

            var fireFrom = fireDate.Date;                 // "уволен с"
            InTransaction((conn, tx) =>
            {
                long ephId;
                DateTime startDate;

                // находим активную запись
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
SELECT Id, StartDate
FROM EmployeePositionsHistory
WHERE EmployeeId=@eid AND EndDate IS NULL
LIMIT 1;";
                    cmd.Parameters.AddWithValue("@eid", employeeId);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            throw new InvalidOperationException("Нечего увольнять: нет активной записи (EndDate IS NULL).");

                        ephId = (long)r["Id"];
                        startDate = DateTime.Parse(r["StartDate"].ToString()).Date;
                    }
                }

                // "уволен с даты" не может быть раньше даты назначения
                if (fireFrom < startDate)
                    throw new InvalidOperationException("Дата увольнения раньше даты назначения на должность.");

                // если "уволен с" = StartDate, то EndDate станет меньше StartDate -> CHECK упадет
                if (fireFrom == startDate)
                    throw new InvalidOperationException("Увольнение 'с даты' в день назначения невозможно. Выберите дату позже (минимум на следующий день).");

                var endWorkDate = fireFrom.AddDays(-1);    // последний рабочий день
                var endText = endWorkDate.ToString("yyyy-MM-dd");

                // закрываем активную запись
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
UPDATE EmployeePositionsHistory
SET EndDate=@ed,
    Action=@act,
    Note=COALESCE(@note, Note)
WHERE Id=@id AND EndDate IS NULL;";
                    cmd.Parameters.AddWithValue("@ed", endText);
                    cmd.Parameters.AddWithValue("@act", (int)DbSchema.EmployeeAction.Fire);
                    cmd.Parameters.AddWithValue("@note", (object)note ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", ephId);

                    var affected = cmd.ExecuteNonQuery();
                    if (affected == 0)
                        throw new InvalidOperationException("Нечего увольнять: нет активной записи (EndDate IS NULL).");
                }
            });
        }
        public DataTable Employee_History(long employeeId)
        {
            return Query(@"
        SELECT
          h.Id,
          p.Name AS Position,
          h.StartDate,
          h.EndDate,
          CASE h.Action
              WHEN 1 THEN 'Прием'
              WHEN 2 THEN 'Перевод'
              WHEN 3 THEN 'Увольнение'
              ELSE 'Неизвестно'
          END AS Action
        FROM EmployeePositionsHistory h
        JOIN Positions p ON p.Id = h.PositionId
        WHERE h.EmployeeId=@e
        ORDER BY date(h.StartDate), h.Id;",
                new SQLiteParameter("@e", employeeId)
            );
        }
        public void Employee_CancelLastOperation(long employeeId)
        {
            if (employeeId <= 0) throw new ArgumentOutOfRangeException(nameof(employeeId));

            InTransaction((conn, tx) =>
            {
                // Есть ли активная запись?
                long? activeId = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
SELECT Id
FROM EmployeePositionsHistory
WHERE EmployeeId=@eid AND EndDate IS NULL
LIMIT 1;";
                    cmd.Parameters.AddWithValue("@eid", employeeId);

                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                        activeId = Convert.ToInt64(obj);
                }

                if (activeId.HasValue)
                {
                    // Значит последнее действие, скорее всего, перевод (или просто текущее назначение).
                    // Пытаемся отменить перевод (вариант А: если есть табель по новой записи — запретим)
                    CancelLastTransfer_Core(conn, tx, employeeId);
                }
                else
                {
                    // Нет активной записи -> сотрудник уволен (последняя запись закрыта)
                    CancelLastFire_Core(conn, tx, employeeId);
                }
            });
        }
        private void CancelLastTransfer_Core(SQLiteConnection conn, SQLiteTransaction tx, long employeeId)
        {
            // берем две последние записи: активную (new) и предыдущую (prev)
            long newEphId, prevEphId;

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
SELECT Id, EndDate
FROM EmployeePositionsHistory
WHERE EmployeeId=@eid
ORDER BY date(StartDate) DESC, Id DESC
LIMIT 2;";
                cmd.Parameters.AddWithValue("@eid", employeeId);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("У сотрудника нет записей истории.");

                    if (r["EndDate"] != DBNull.Value)
                        throw new InvalidOperationException("Нет активной записи — отмена перевода невозможна.");

                    newEphId = Convert.ToInt64(r["Id"]);

                    if (!r.Read())
                        throw new InvalidOperationException("Нечего отменять: нет предыдущей записи до перевода.");

                    prevEphId = Convert.ToInt64(r["Id"]);
                }
            }

            // Вариант А: если по новой записи уже есть табель — запрещаем отмену
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
SELECT COUNT(1)
FROM TimesheetEntry
WHERE EmployeePositionsHistoryId=@eph;";
                cmd.Parameters.AddWithValue("@eph", newEphId);

                int cnt = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                if (cnt > 0)
                    throw new InvalidOperationException(
                        $"Нельзя отменить перевод: по новой должности уже внесён табель ({cnt} записей). " +
                        "Сначала удалите часы/записи по новой должности.");
            }

            // удаляем новую активную запись
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM EmployeePositionsHistory WHERE Id=@id;";
                cmd.Parameters.AddWithValue("@id", newEphId);
                cmd.ExecuteNonQuery();
            }

            // открываем предыдущую
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE EmployeePositionsHistory SET EndDate=NULL WHERE Id=@id;";
                cmd.Parameters.AddWithValue("@id", prevEphId);
                cmd.ExecuteNonQuery();
            }
        }
        private void CancelLastFire_Core(SQLiteConnection conn, SQLiteTransaction tx, long employeeId)
        {
            long lastEphId;
            object endObj;
            object actionObj;

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
SELECT Id, EndDate, Action
FROM EmployeePositionsHistory
WHERE EmployeeId=@eid
ORDER BY date(StartDate) DESC, Id DESC
LIMIT 1;";
                cmd.Parameters.AddWithValue("@eid", employeeId);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                        throw new InvalidOperationException("У сотрудника нет записей истории.");

                    lastEphId = Convert.ToInt64(r["Id"]);
                    endObj = r["EndDate"];
                    actionObj = r["Action"];
                }
            }

            if (endObj == DBNull.Value)
                throw new InvalidOperationException("Сотрудник не уволен (есть активная запись).");

            int action = Convert.ToInt32(actionObj ?? 0);
            if (action != (int)DbSchema.EmployeeAction.Fire)
                throw new InvalidOperationException("Последняя операция не является увольнением — отмена увольнения невозможна.");

            // делаем запись снова активной
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE EmployeePositionsHistory
SET EndDate=NULL
WHERE Id=@id;";
                cmd.Parameters.AddWithValue("@id", lastEphId);
                cmd.ExecuteNonQuery();
            }
        }
        #endregion
        #region Timesheet
        public DataTable GetMainGrid(int year, int month, int departmentId)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEndExcl = monthStart.AddMonths(1);
            var monthEndIncl = monthEndExcl.AddDays(-1);

            // 1) Таблица под грид
            var dt = new DataTable();
            dt.Columns.Add("Department", typeof(string));
            dt.Columns.Add("Position", typeof(string));
            dt.Columns.Add("EmployeeId", typeof(int));
            dt.Columns.Add("EPHId", typeof(int));
            dt.Columns.Add("Fio", typeof(string));
            for (int i = 1; i <= 31; i++) dt.Columns.Add("D" + i, typeof(string));
            dt.Columns.Add("TotalProgram", typeof(string));
            dt.Columns.Add("TotalAll", typeof(string));

            // 2) Забираем EPH строки (периоды)
            var ephRows = Query(@"
SELECT
  eph.Id            AS EPHId,
  e.Id              AS EmployeeId,
  e.Fio             AS Fio,
  d.Name            AS Department,
  p.Name            AS Position,
  eph.StartDate     AS StartDate,
  eph.EndDate       AS EndDate
FROM EmployeePositionsHistory eph
JOIN Employees e   ON e.Id = eph.EmployeeId
JOIN Departments d ON d.Id = eph.DepartmentId
JOIN Positions p   ON p.Id = eph.PositionId
WHERE date(eph.StartDate) < date(@MonthEndExcl)
  AND (eph.EndDate IS NULL OR date(eph.EndDate) >= date(@MonthStart))
ORDER BY e.Fio, date(eph.StartDate);
",
                new SQLiteParameter("@MonthStart", monthStart.ToString("yyyy-MM-dd")),
                new SQLiteParameter("@MonthEndExcl", monthEndExcl.ToString("yyyy-MM-dd"))
            );

            // 3) Индекс строк грида по EPHId
            var map = new Dictionary<int, DataRow>();

            foreach (DataRow r in ephRows.Rows)
            {
                int ephId = Convert.ToInt32(r["EPHId"]);
                int empId = Convert.ToInt32(r["EmployeeId"]);

                DateTime start = DateTime.Parse(r["StartDate"].ToString());
                DateTime? end = r["EndDate"] == DBNull.Value ? (DateTime?)null : DateTime.Parse(r["EndDate"].ToString());

                var activeStart = start < monthStart ? monthStart : start;
                var activeEnd = end == null ? monthEndIncl : (end.Value > monthEndIncl ? monthEndIncl : end.Value);

                var row = dt.NewRow();
                row["Department"] = r["Department"].ToString();
                row["Position"] = r["Position"].ToString();
                row["EmployeeId"] = empId;
                row["EPHId"] = ephId;
                row["Fio"] = r["Fio"].ToString();

                // заполняем днями: в НЕ-активных ставим "—", в активных пока пусто
                for (int day = 1; day <= DateTime.DaysInMonth(year, month); day++)
                {
                    var d = new DateTime(year, month, day);
                    row["D" + day] = (d < activeStart || d > activeEnd) ? "—" : "";
                }

                // дни 29..31 в коротких месяцах можно тоже "—" (чтобы красиво)
                for (int day = DateTime.DaysInMonth(year, month) + 1; day <= 31; day++)
                    row["D" + day] = "—";

                row["TotalProgram"] = "0";
                row["TotalAll"] = "0";

                dt.Rows.Add(row);
                map[ephId] = row;
            }

            // 4) Забираем суммы по дням (привязаны к EPHId)
            var entries = Query(@"
SELECT
  tse.EmployeePositionsHistoryId AS EPHId,
  tse.WorkDate                   AS WorkDate,
  SUM(tse.Minutes)               AS MinutesSum
FROM TimesheetEntry tse
JOIN Timesheet t ON t.Id = tse.TimesheetId
WHERE t.Year = @Year AND t.Month = @Month
  AND t.DepartmentId = @DepartmentId
GROUP BY tse.EmployeePositionsHistoryId, tse.WorkDate;
",
                new SQLiteParameter("@Year", year),
                new SQLiteParameter("@Month", month),
                new SQLiteParameter("@DepartmentId", departmentId)
            );

            // 5) Раскладываем в нужную строку (EPHId) и считаем итоги
            var totalAllByEph = new Dictionary<int, int>();

            foreach (DataRow e in entries.Rows)
            {
                int ephId = Convert.ToInt32(e["EPHId"]);
                if (!map.TryGetValue(ephId, out var row)) continue;

                var workDate = DateTime.Parse(e["WorkDate"].ToString());
                int mins = Convert.ToInt32(e["MinutesSum"]);

                int day = workDate.Day;
                var cellName = "D" + day;

                // если там "—", значит запись не должна сюда попадать (но на всякий случай не пишем)
                if ((row[cellName] as string) == "—") continue;

                row[cellName] = mins.ToString(); // или формат в часы
                totalAllByEph[ephId] = (totalAllByEph.TryGetValue(ephId, out var cur) ? cur : 0) + mins;
            }

            foreach (var kv in totalAllByEph)
            {
                var row = map[kv.Key];
                row["TotalAll"] = kv.Value.ToString();
                // TotalProgram — если нужно “по выбранной программе”, тогда надо фильтр ProgramId и второй подсчет
            }

            return dt;
        }
        public DataTable Employee_AssignmentsForMonth(long rootDepartmentId, int year, int month, bool includeFired)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            // Вариант А (минимально): includeFired влияет только на "уволен до месяца" — но это уже отсекает WHERE.
            // Поэтому можно оставить пустым.
            string firedFilter = "";

            // Вариант Б (если ты хочешь НЕ показывать сегменты, которые являются увольнением):
            // string firedFilter = includeFired ? "" : "AND h.Action <> 3";

            return Query($@"
WITH RECURSIVE dept(Id) AS (
    SELECT @root
    UNION ALL
    SELECT d.Id
    FROM Departments d
    JOIN dept ON d.ParentId = dept.Id
)
SELECT
    h.Id AS EPHId,                         -- <-- ВАЖНО
    e.Id AS EmployeeId,
    e.Fio AS Fio,
    h.DepartmentId AS DepartmentId,
    d.Name AS DepartmentName,
    h.PositionId AS PositionId,
    p.Name AS PositionName,

    CASE WHEN date(h.StartDate) > date(@monthStart)
         THEN date(h.StartDate) ELSE date(@monthStart) END AS SegStart,

    CASE WHEN date(COALESCE(h.EndDate, @monthEnd)) < date(@monthEnd)
         THEN date(COALESCE(h.EndDate, @monthEnd)) ELSE date(@monthEnd) END AS SegEnd
FROM EmployeePositionsHistory h
JOIN Employees e ON e.Id = h.EmployeeId
JOIN Departments d ON d.Id = h.DepartmentId
JOIN Positions p ON p.Id = h.PositionId
WHERE h.DepartmentId IN (SELECT Id FROM dept)
  AND date(h.StartDate) <= date(@monthEnd)
  AND date(COALESCE(h.EndDate, @monthEnd)) >= date(@monthStart)
  {firedFilter}
ORDER BY e.Fio, SegStart, d.Name, p.Name;",
                new SQLiteParameter("@root", rootDepartmentId),
                new SQLiteParameter("@monthStart", monthStart),
                new SQLiteParameter("@monthEnd", monthEnd)
            );
        }
        public DataTable TimesheetMinutes_ByEphDay(long rootDepartmentId, int year, int month, int programId)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            return Query(@"
WITH RECURSIVE dept(Id) AS (
    SELECT @root
    UNION ALL
    SELECT d.Id
    FROM Departments d
    JOIN dept ON d.ParentId = dept.Id
)
SELECT
    te.EmployeePositionsHistoryId AS EPHId,
    date(te.WorkDate) AS WorkDate,
    SUM(te.Minutes) AS MinAll,
    SUM(CASE WHEN @p = 0 OR te.ProgramId = @p THEN te.Minutes ELSE 0 END) AS MinProg
FROM TimesheetEntry te
JOIN Timesheet ts ON ts.Id = te.TimesheetId
WHERE ts.DepartmentId IN (SELECT Id FROM dept)
  AND date(te.WorkDate) BETWEEN date(@monthStart) AND date(@monthEnd)
GROUP BY te.EmployeePositionsHistoryId, date(te.WorkDate)
ORDER BY te.EmployeePositionsHistoryId, WorkDate;",
                new SQLiteParameter("@root", rootDepartmentId),
                new SQLiteParameter("@monthStart", monthStart),
                new SQLiteParameter("@monthEnd", monthEnd),
                new SQLiteParameter("@p", programId)
            );
        }
        public DataTable TimesheetMinutes_ByEphProgramWorkDay(long ephId, int year, int month, int programId)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            return Query(@"
SELECT
    te.WorkId AS WorkId,
    date(te.WorkDate) AS WorkDate,
    SUM(te.Minutes) AS MinSum
FROM TimesheetEntry te
JOIN Timesheet ts ON ts.Id = te.TimesheetId
WHERE te.EmployeePositionsHistoryId = @eph
  AND te.ProgramId = @p
  AND date(te.WorkDate) BETWEEN date(@monthStart) AND date(@monthEnd)
GROUP BY te.WorkId, date(te.WorkDate)
ORDER BY te.WorkId, WorkDate;",
                new SQLiteParameter("@eph", ephId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@monthStart", monthStart),
                new SQLiteParameter("@monthEnd", monthEnd)
            );
        }
        public DataTable ListOfWork_UsedForEphProgramMonth(long ephId, int year, int month, int programId)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            return Query(@"
SELECT
    w.Id AS WorkId,
    CASE
        WHEN w.SpecialCode IS NULL OR TRIM(w.SpecialCode) = ''
        THEN w.Name
        ELSE w.SpecialCode || ' — ' || w.Name
    END AS WorkName
FROM TimesheetEntry te
JOIN ListOfWork w ON w.Id = te.WorkId
WHERE te.EmployeePositionsHistoryId = @eph
  AND te.ProgramId = @p
  AND date(te.WorkDate) BETWEEN date(@monthStart) AND date(@monthEnd)
GROUP BY w.Id, w.Name
ORDER BY w.Name;",
                new SQLiteParameter("@eph", ephId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@monthStart", monthStart),
                new SQLiteParameter("@monthEnd", monthEnd)
            );
        }
        public long Timesheet_GetOrCreate(int year, int month, long departmentId)
        {
            return InTransactionReturn((conn, tx) =>
            {
                // ищем
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"SELECT Id FROM Timesheet WHERE Year=@y AND Month=@m AND DepartmentId=@d LIMIT 1;";
                    cmd.Parameters.AddWithValue("@y", year);
                    cmd.Parameters.AddWithValue("@m", month);
                    cmd.Parameters.AddWithValue("@d", departmentId);

                    var idObj = cmd.ExecuteScalar();
                    if (idObj != null && idObj != DBNull.Value)
                        return Convert.ToInt64(idObj);
                }

                // создаём
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO Timesheet(Year,Month,DepartmentId,CreatedAt)
                                VALUES(@y,@m,@d,@ca);
                                SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@y", year);
                    cmd.Parameters.AddWithValue("@m", month);
                    cmd.Parameters.AddWithValue("@d", departmentId);
                    cmd.Parameters.AddWithValue("@ca", DateTime.Now.ToString("yyyy-MM-dd"));

                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            });
        }
        public void TimesheetEntry_UpsertByEph(long timesheetId, DateTime workDate, long ephId, int programId, long workId, int minutes)
        {
            Execute(@"
INSERT INTO TimesheetEntry(TimesheetId, WorkDate, EmployeePositionsHistoryId, ProgramId, WorkId, Minutes, Note)
VALUES(@ts, @d, @eph, @p, @w, @min, NULL)
ON CONFLICT(TimesheetId, WorkDate, EmployeePositionsHistoryId, ProgramId, WorkId)
DO UPDATE SET Minutes=excluded.Minutes;",
                new SQLiteParameter("@ts", timesheetId),
                new SQLiteParameter("@d", workDate.ToString("yyyy-MM-dd")),
                new SQLiteParameter("@eph", ephId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@w", workId),
                new SQLiteParameter("@min", minutes)
            );
        }
        public (DateTime segStart, DateTime segEnd) EPH_GetSegmentForMonth(long ephId, int year, int month)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            var dt = Query(@"
SELECT
  CASE WHEN date(h.StartDate) > date(@ms) THEN date(h.StartDate) ELSE date(@ms) END AS SegStart,
  CASE WHEN date(COALESCE(h.EndDate, @me)) < date(@me) THEN date(COALESCE(h.EndDate, @me)) ELSE date(@me) END AS SegEnd
FROM EmployeePositionsHistory h
WHERE h.Id = @id
LIMIT 1;",
                new SQLiteParameter("@id", ephId),
                new SQLiteParameter("@ms", monthStart),
                new SQLiteParameter("@me", monthEnd)
            );

            if (dt.Rows.Count == 0) throw new InvalidOperationException("EPH not found: " + ephId);

            var r = dt.Rows[0];
            return (DateTime.Parse(r["SegStart"].ToString()), DateTime.Parse(r["SegEnd"].ToString()));
        }
        public int TimesheetEntry_CountForEphProgramWorkMonth(long ephId, int year, int month, int programId, long workId)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            var obj = Scalar(@"
SELECT COUNT(1)
FROM TimesheetEntry
WHERE EmployeePositionsHistoryId = @eph
  AND ProgramId = @p
  AND WorkId = @w
  AND date(WorkDate) BETWEEN date(@ms) AND date(@me);",
                new SQLiteParameter("@eph", ephId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@w", workId),
                new SQLiteParameter("@ms", monthStart),
                new SQLiteParameter("@me", monthEnd)
            );

            return Convert.ToInt32(obj ?? 0);
        }
        public int TimesheetEntry_DeleteForEphProgramWorkMonth(long ephId, int year, int month, int programId, long workId)
        {
            string monthStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            string monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            return Execute(@"
DELETE FROM TimesheetEntry
WHERE EmployeePositionsHistoryId = @eph
  AND ProgramId = @p
  AND WorkId = @w
  AND date(WorkDate) BETWEEN date(@ms) AND date(@me);",
                new SQLiteParameter("@eph", ephId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@w", workId),
                new SQLiteParameter("@ms", monthStart),
                new SQLiteParameter("@me", monthEnd)
            );
        }
        public DataTable TimesheetEntry_WeeksWithHoursForProgram(int programId)
        {
            return Query(@"
SELECT
  date(te.WorkDate, '-' || ((cast(strftime('%w', te.WorkDate) as integer) + 6) % 7) || ' days') AS WeekMonday,
  SUM(te.Minutes) AS SumMinutes
FROM TimesheetEntry te
WHERE te.ProgramId = @p
GROUP BY WeekMonday
HAVING SUM(te.Minutes) > 0
ORDER BY WeekMonday;",
                new SQLiteParameter("@p", programId)
            );
        }
        #endregion
        #region ReportDepartmentFunctions
        public (string Name, string ShortName, DateTime Start, DateTime? End)? Program_GetInfo(int id)
        {
            var dt = Query(@"
SELECT Name, ShortName, DateStart, DateEnd
FROM Programs
WHERE Id=@id
LIMIT 1;",
                new SQLiteParameter("@id", id));

            if (dt.Rows.Count == 0) return null;

            var r = dt.Rows[0];
            string name = Convert.ToString(r["Name"]);
            string shortName = Convert.ToString(r["ShortName"]);

            DateTime start = DateTime.Parse(Convert.ToString(r["DateStart"]));
            DateTime? end = null;

            if (r["DateEnd"] != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(r["DateEnd"])))
                end = DateTime.Parse(Convert.ToString(r["DateEnd"]));

            return (name, shortName, start, end);
        }
        public (string ComplexName, string DepartmentName) Department_GetComplexAndDepartmentNames(long departmentLevel3Id)
        {
            var dt = Query(@"
SELECT
  d3.Name AS DepartmentName,
  d2.Name AS ComplexName
FROM Departments d3
LEFT JOIN Departments d2 ON d2.Id = d3.ParentId
WHERE d3.Id = @id
LIMIT 1;",
                new SQLiteParameter("@id", departmentLevel3Id));

            if (dt.Rows.Count == 0)
                throw new InvalidOperationException("Department not found: " + departmentLevel3Id);

            return (
                Convert.ToString(dt.Rows[0]["ComplexName"]) ?? "",
                Convert.ToString(dt.Rows[0]["DepartmentName"]) ?? ""
            );
        }
        public DataTable Employee_AssignmentsForPeriod(long rootDepartmentId, DateTime periodStart, DateTime periodEnd, bool includeFired)
        {
            string ps = periodStart.ToString("yyyy-MM-dd");
            string pe = periodEnd.ToString("yyyy-MM-dd");

            // Сейчас includeFired не режем — логика “уволен/не уволен” определяется пересечением дат.
            // Если потом захочешь исключать сегменты увольнения — добавим фильтр по Action.
            string firedFilter = ""; // includeFired ? "" : "AND h.Action <> 3";  // опционально

            return Query($@"
WITH RECURSIVE dept(Id) AS (
    SELECT @root
    UNION ALL
    SELECT d.Id
    FROM Departments d
    JOIN dept ON d.ParentId = dept.Id
)
SELECT
    h.Id AS EPHId,
    e.Id AS EmployeeId,
    e.Fio AS Fio,
    h.DepartmentId AS DepartmentId,
    d.Name AS DepartmentName,
    h.PositionId AS PositionId,
    p.Name AS PositionName,

    CASE WHEN date(h.StartDate) > date(@ps)
         THEN date(h.StartDate) ELSE date(@ps) END AS SegStart,

    CASE WHEN date(COALESCE(h.EndDate, @pe)) < date(@pe)
         THEN date(COALESCE(h.EndDate, @pe)) ELSE date(@pe) END AS SegEnd
FROM EmployeePositionsHistory h
JOIN Employees e ON e.Id = h.EmployeeId
JOIN Departments d ON d.Id = h.DepartmentId
JOIN Positions p ON p.Id = h.PositionId
WHERE h.DepartmentId IN (SELECT Id FROM dept)
  AND date(h.StartDate) <= date(@pe)
  AND date(COALESCE(h.EndDate, @pe)) >= date(@ps)
  {firedFilter}
ORDER BY e.Fio, date(SegStart), d.Name, p.Name, h.Id;",
                new SQLiteParameter("@root", rootDepartmentId),
                new SQLiteParameter("@ps", ps),
                new SQLiteParameter("@pe", pe)
            );
        }
        public DataTable Employees_WorkedForProgramPeriod(long rootDepartmentId, int programId, DateTime periodStart, DateTime periodEnd)
        {
            string ps = periodStart.ToString("yyyy-MM-dd");
            string pe = periodEnd.ToString("yyyy-MM-dd");

            return Query(@"
WITH RECURSIVE dept(Id) AS (
    SELECT @root
    UNION ALL
    SELECT d.Id
    FROM Departments d
    JOIN dept ON d.ParentId = dept.Id
),
workedEph AS (
    SELECT DISTINCT te.EmployeePositionsHistoryId AS EPHId
    FROM TimesheetEntry te
    JOIN Timesheet ts ON ts.Id = te.TimesheetId
    WHERE ts.DepartmentId IN (SELECT Id FROM dept)
      AND te.ProgramId = @p
      AND date(te.WorkDate) BETWEEN date(@ps) AND date(@pe)
      AND te.Minutes > 0
)
SELECT
    h.Id AS EPHId,
    e.Id AS EmployeeId,
    e.Fio AS Fio,
    h.DepartmentId AS DepartmentId,
    d.Name AS DepartmentName,
    h.PositionId AS PositionId,
    p.Name AS PositionName,

    -- сегмент в рамках выбранного периода (может пригодиться позже)
    CASE WHEN date(h.StartDate) > date(@ps)
         THEN date(h.StartDate) ELSE date(@ps) END AS SegStart,

    CASE WHEN date(COALESCE(h.EndDate, @pe)) < date(@pe)
         THEN date(COALESCE(h.EndDate, @pe)) ELSE date(@pe) END AS SegEnd
FROM workedEph w
JOIN EmployeePositionsHistory h ON h.Id = w.EPHId
JOIN Employees e ON e.Id = h.EmployeeId
JOIN Departments d ON d.Id = h.DepartmentId
JOIN Positions p ON p.Id = h.PositionId
ORDER BY e.Fio, date(SegStart), d.Name, p.Name, h.Id;",
                new SQLiteParameter("@root", rootDepartmentId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@ps", ps),
                new SQLiteParameter("@pe", pe)
            );
        }
        public bool HasHoursForDepartmentProgramPeriod(long rootDepartmentId, int programId, DateTime periodStart, DateTime periodEnd)
        {
            string ps = periodStart.ToString("yyyy-MM-dd");
            string pe = periodEnd.ToString("yyyy-MM-dd");

            var obj = Scalar(@"
WITH RECURSIVE dept(Id) AS (
    SELECT @root
    UNION ALL
    SELECT d.Id
    FROM Departments d
    JOIN dept ON d.ParentId = dept.Id
)
SELECT COUNT(1)
FROM TimesheetEntry te
JOIN Timesheet ts ON ts.Id = te.TimesheetId
WHERE ts.DepartmentId IN (SELECT Id FROM dept)
  AND te.ProgramId = @p
  AND date(te.WorkDate) BETWEEN date(@ps) AND date(@pe)
  AND te.Minutes > 0;",
                new SQLiteParameter("@root", rootDepartmentId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@ps", ps),
                new SQLiteParameter("@pe", pe)
            );

            return Convert.ToInt32(obj ?? 0) > 0;
        }
        public DataTable ListOfWork_UsedForEphProgramPeriod(long ephId, int programId, DateTime periodStart, DateTime periodEnd)
        {
            string ps = periodStart.ToString("yyyy-MM-dd");
            string pe = periodEnd.ToString("yyyy-MM-dd");

            return Query(@"
SELECT
    w.Id AS WorkId,
    w.SpecialCode AS SpecialCode,
    w.Name AS Name
FROM TimesheetEntry te
JOIN ListOfWork w ON w.Id = te.WorkId
WHERE te.EmployeePositionsHistoryId = @eph
  AND te.ProgramId = @p
  AND te.Minutes > 0
  AND date(te.WorkDate) BETWEEN date(@ps) AND date(@pe)
GROUP BY w.Id, w.SpecialCode, w.Name
ORDER BY
  CASE WHEN TRIM(COALESCE(w.SpecialCode,'')) = '' THEN 1 ELSE 0 END,
  w.SpecialCode, w.Name;",
                new SQLiteParameter("@eph", ephId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@ps", ps),
                new SQLiteParameter("@pe", pe)
            );
        }
        public DataTable TimesheetMinutes_ByEphProgramWorkDayPeriod(long ephId, int programId, DateTime periodStart, DateTime periodEnd)
        {
            string ps = periodStart.ToString("yyyy-MM-dd");
            string pe = periodEnd.ToString("yyyy-MM-dd");

            return Query(@"
SELECT
    te.WorkId AS WorkId,
    date(te.WorkDate) AS WorkDate,
    SUM(te.Minutes) AS MinSum
FROM TimesheetEntry te
WHERE te.EmployeePositionsHistoryId = @eph
  AND te.ProgramId = @p
  AND te.Minutes > 0
  AND date(te.WorkDate) BETWEEN date(@ps) AND date(@pe)
GROUP BY te.WorkId, date(te.WorkDate)
ORDER BY te.WorkId, WorkDate;",
                new SQLiteParameter("@eph", ephId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@ps", ps),
                new SQLiteParameter("@pe", pe)
            );
        }
        public DataTable TimesheetMinutes_ByDeptTree_Program_WorkDayPeriod(
    long rootDepartmentId,
    int programId,
    DateTime periodStart,
    DateTime periodEnd)
        {
            string ps = periodStart.ToString("yyyy-MM-dd");
            string pe = periodEnd.ToString("yyyy-MM-dd");

            return Query(@"
WITH RECURSIVE dept(Id) AS (
    SELECT @root
    UNION ALL
    SELECT d.Id
    FROM Departments d
    JOIN dept ON d.ParentId = dept.Id
)
SELECT
    w.Id AS WorkId,
    w.SpecialCode AS SpecialCode,
    w.Name AS Name,
    date(te.WorkDate) AS WorkDate,
    SUM(te.Minutes) AS MinSum
FROM TimesheetEntry te
JOIN Timesheet ts ON ts.Id = te.TimesheetId
JOIN ListOfWork w ON w.Id = te.WorkId
WHERE ts.DepartmentId IN (SELECT Id FROM dept)
  AND te.ProgramId = @p
  AND te.Minutes > 0
  AND date(te.WorkDate) BETWEEN date(@ps) AND date(@pe)
GROUP BY w.Id, w.SpecialCode, w.Name, date(te.WorkDate)
ORDER BY
  CASE WHEN TRIM(COALESCE(w.SpecialCode,'')) = '' THEN 1 ELSE 0 END,
  w.SpecialCode, w.Name, date(te.WorkDate);",
                new SQLiteParameter("@root", rootDepartmentId),
                new SQLiteParameter("@p", programId),
                new SQLiteParameter("@ps", ps),
                new SQLiteParameter("@pe", pe)
            );
        }
        #endregion
        #region ReportComplexFunctions
        public DataTable Departments_ListOnlyLevel2()
        {
            return Query(@"
        SELECT 
            Id,
            Name AS DisplayName
        FROM Departments
        WHERE Level = 2
        ORDER BY Name;
    ");
        }
        #endregion
    }
}
