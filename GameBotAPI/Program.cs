using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Telegram.Bot;
using GameBotAPI;
using GameBotAPI.Services;
using UnoGame.Telegram;
using UnoGame;
using CommonGameLib.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddControllers().AddNewtonsoftJson();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var botConfig = builder.Configuration.GetSection("BotConfiguration").Get<BotConfiguration>();
builder.Services.AddHostedService<TGUnoBotConfigService>();

//builder.Services.AddHttpClient("BoardGameBot")
//       .AddTypedClient<ITelegramBotClient>(httpClient => new TelegramBotClient(botConfig.UnoTGBotToken, httpClient));

builder.Services.AddScoped<IUnoTGBotService>(x => new UnoTGBotService(
    x.GetRequiredService<IGameService>(),
    x.GetRequiredService<ICachedService>(),
    x.GetRequiredService<ILogger<UnoTGBotService>>(),
    botConfig.UnoTGBotToken)); ;

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<ICachedService, CachedService>();
builder.Services.AddSingleton<IGameService, GameService>();
builder.Services.AddDistributedMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
