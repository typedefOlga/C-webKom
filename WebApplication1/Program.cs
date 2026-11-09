
var builder = WebApplication.CreateBuilder(args);

// Подключаем контроллеры.
builder.Services.AddControllers();

// Подключаем Swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// В режиме разработки доступен Swagger.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program { }