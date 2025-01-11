using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<UserService>();

var app = builder.Build();

// Error Handling Middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new { error = "Internal server error." };
        var jsonResponse = JsonSerializer.Serialize(response);

        // Log the exception (logging code omitted for brevity)
        Console.WriteLine($"Exception: {ex.Message}");

        await context.Response.WriteAsync(jsonResponse);
    }
});

// Authentication Middleware
app.Use(async (context, next) =>
{
    var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

    if (token != null && ValidateToken(token))
    {
        await next();
    }
    else
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

        var response = new { error = "Unauthorized" };
        var jsonResponse = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(jsonResponse);
    }
});

// Logging Middleware
app.Use(async (context, next) =>
{
    // Log request details
    var method = context.Request.Method;
    var path = context.Request.Path;
    Console.WriteLine($"Request: {method} {path}");

    await next();

    // Log response status code
    var statusCode = context.Response.StatusCode;
    Console.WriteLine($"Response: {statusCode}");
    // Log the request and response details (logging code omitted for brevity)
});


app.MapGet("/users", (UserService userService) =>
{
    return Results.Ok(userService.GetUsers());
});

app.MapGet("/users/{id:int}", (int id, UserService userService) =>
{
    var user = userService.GetUser(id);
    return user is not null ? Results.Ok(user) : Results.NotFound(new { Message = "User not found" });
});

app.MapPost("/users", (User user, UserService userService) =>
{
    if (!ValidateUser(user))
    {
        return Results.BadRequest(new { Message = "Invalid user data" });
    }

    var newUser = userService.AddUser(user);
    return Results.Created($"/users/{newUser.Id}", newUser);
});

app.MapPut("/users/{id:int}", (int id, User updatedUser, UserService userService) =>
{
    if (!ValidateUser(updatedUser))
    {
        return Results.BadRequest(new { Message = "Invalid user data" });
    }

    var user = userService.UpdateUser(id, updatedUser);
    return user is not null ? Results.Ok(user) : Results.NotFound(new { Message = "User not found" });
});

app.MapDelete("/users/{id:int}", (int id, UserService userService) =>
{
    return userService.DeleteUser(id) ? Results.Ok(new { Message = "User deleted" }) : Results.NotFound(new { Message = "User not found" });
});

app.Run();

bool ValidateUser(User user)
{
    return !string.IsNullOrEmpty(user.Name) && !string.IsNullOrEmpty(user.Email);
}

bool ValidateToken(string token)
{
    // Token validation logic (simplified for brevity)
    return token == "valid-token"; // Replace with actual validation logic
}

class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

class UserService
{
    private readonly ConcurrentDictionary<int, User> _users = new();

    public IEnumerable<User> GetUsers() => _users.Values;

    public User GetUser(int id) => _users.GetValueOrDefault(id);

    public User AddUser(User user)
    {
        user.Id = _users.Count > 0 ? _users.Keys.Max() + 1 : 1;
        _users.TryAdd(user.Id, user);
        return user;
    }

    public User UpdateUser(int id, User updatedUser)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.Name = updatedUser.Name;
            user.Email = updatedUser.Email;
            return user;
        }
        return null;
    }

    public bool DeleteUser(int id) => _users.TryRemove(id, out _);
}
