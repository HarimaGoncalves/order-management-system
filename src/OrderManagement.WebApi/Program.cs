using OrderManagement.Application;
using OrderManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// Clean Architecture: each layer registers its own dependencies
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redirect root URL to Swagger UI
    app.MapGet("/", context => Task.Run(() => context.Response.Redirect("/swagger/index.html")));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
