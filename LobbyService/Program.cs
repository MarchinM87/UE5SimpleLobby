using LobbyService.Models;
using LobbyService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// ── 注册服务 ────────────────────────────────────────────
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册 RoomStore 为单例
builder.Services.AddSingleton<RoomStore>();
builder.Services.Configure<DedicatedServerOptions>(
    builder.Configuration.GetSection(DedicatedServerOptions.SectionName));
builder.Services.AddSingleton<IDedicatedServerManager, DedicatedServerManager>();

// CORS（允许 UE 客户端跨域请求）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseAuthorization();
app.UseCors();

app.MapControllers();

// 监听地址（跨平台通用）
app.Run("http://0.0.0.0:5000");
