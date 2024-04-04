using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Auth.DB
{
    public class MyDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public MyDbContext(DbContextOptions<MyDbContext> options)
            : base(options)
        {
        }
    }
}
