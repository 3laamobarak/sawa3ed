using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Infrastructure.Persistence;

internal sealed class DesignActor : ICurrentUser { public string? UserId => null; }
