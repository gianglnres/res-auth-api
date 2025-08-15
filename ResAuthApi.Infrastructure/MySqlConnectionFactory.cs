using MySql.Data.MySqlClient;
using System.Data;

namespace ResAuthApi.Infrastructure
{
    public class MySqlConnectionFactory
    {
        private readonly string _conn;
        public MySqlConnectionFactory(string conn) => _conn = conn;
        public IDbConnection Create() => new MySqlConnection(_conn);
    }
}
