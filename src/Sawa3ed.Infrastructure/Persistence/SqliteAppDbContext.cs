using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options, TimeProvider clock, ICurrentUser actor)
    : AppDbContext(options, clock, actor);
