using Bkl.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Bkl.Infrastructure;
using System.Threading.Channels;

namespace Bkl.ESPS
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var default1 = JwtSecurityTokenHandler.DefaultInboundClaimTypeMap;
            BklConfig config = new BklConfig();
            Configuration.GetSection("BklConfig").Bind(config);
            var dbHost = Environment.GetEnvironmentVariable("BKL_DB_HOST");
            var dbName = Environment.GetEnvironmentVariable("BKL_DB_NAME");
            var minioEndPoint = Environment.GetEnvironmentVariable("BKL_MINIO_ENDPOINT");
            var minioPublicEndPoint = Environment.GetEnvironmentVariable("BKL_MINIO_PUBLICENDPOINT");
            var minioKey = Environment.GetEnvironmentVariable("BKL_MINIO_KEY");
            var minioSecret = Environment.GetEnvironmentVariable("BKL_MINIO_SECRET");
            var minioRegion = Environment.GetEnvironmentVariable("BKL_MINIO_REGION");
            config.DatabaseConfig.host = string.IsNullOrEmpty(dbHost) ? config.DatabaseConfig.host : dbHost;
            config.DatabaseConfig.database = string.IsNullOrEmpty(dbName) ? config.DatabaseConfig.database : dbName;
            config.MinioConfig.EndPoint = string.IsNullOrEmpty(minioEndPoint) ? config.MinioConfig.EndPoint : minioEndPoint;
            config.MinioConfig.PublicEndPoint = string.IsNullOrEmpty(minioPublicEndPoint) ? config.MinioConfig.PublicEndPoint : minioPublicEndPoint;

            config.MinioConfig.Key = string.IsNullOrEmpty(minioKey) ? config.MinioConfig.Key : minioKey;
            config.MinioConfig.Secret = string.IsNullOrEmpty(minioSecret) ? config.MinioConfig.Secret : minioSecret;
            config.MinioConfig.Region = string.IsNullOrEmpty(minioRegion) ? config.MinioConfig.Region : minioRegion;

            services.AddRedis(config);
            services.AddSingleton(config);
            services.AddSnowId(config);


            Console.WriteLine($"env {dbHost} {dbName} , mysql {config.MySqlString}");
            services.AddDbContext<BklDbContext>((serviceProvider, builder) =>
            {
                builder.UseMySQL(config.MySqlString);
            });
            services.AddDbContext<BklLocalDbContext>(builder => { builder.UseSqlite("Data Source=yolo.db"); });

            services.AddHostedService<ELDetectImageService>();
            services.AddHostedService<ELSegImageService>();
            services.AddScoped<ELDetectHelper>();
            services.AddSingleton<IBackgroundTaskQueue<ELSegImage>>(ctx => new BackgroundTaskQueue<ELSegImage>(1000));
            services.AddSingleton<IBackgroundTaskQueue<ELDetectTaskInfo>>(ctx => new ELDetectTaskQueue(1000));



            services.AddSingleton<IBackgroundTaskQueue<SegTaskInfo>>(ctx => new SegTaskQueue(1000));
            services.AddSingleton<IBackgroundTaskQueue<FuseTaskInfo>>(ctx => new FuseTaskQueue(1000));
            services.AddSingleton<IBackgroundTaskQueue<GenerateAllTaskRequest>>(ctx => new WordTaskQueue(100));
            services.AddSingleton<IBackgroundTaskQueue<DetectTaskInfo>>(ctx => new DetectTaskQueue(1000));
            //services.AddHostedService<BladeReportGenerateService>();
            //services.AddHostedService<DetectImageService>();
            //services.AddHostedService<SegImageService>();
            //services.AddHostedService<FuseImageService>();



            services.AddSingleton(ctx =>
            {
                return Channel.CreateBounded<PowerDetectService.PowerTask>(new BoundedChannelOptions(1000) { Capacity = 1000, FullMode = BoundedChannelFullMode.Wait });
            });
            services.AddSingleton(ctx =>
            {
                return Channel.CreateBounded<PowerReportGenerateService.PowerTask>(new BoundedChannelOptions(1000) { Capacity = 1000, FullMode = BoundedChannelFullMode.Wait });
            });
            services.AddHostedService<PowerDetectService>();
            services.AddHostedService<PowerReportGenerateService>();






            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<LogonUser>(context =>
            {
                var http = context.GetService<IHttpContextAccessor>();
                return new LogonUser(http);
            });
            services.AddScoped<CommonDeviceImport>();


            services.AddCors(option =>
                option.AddPolicy("cors", policy => policy.WithMethods("GET", "POST", "HEAD", "PUT", "DELETE", "OPTIONS")
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .SetIsOriginAllowed(str => true)));
            services.AddAuthentication(auth =>
                {
                    auth.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    auth.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(option =>
                {
                    option.RequireHttpsMetadata = false;
                    option.SaveToken = true;
                    option.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.AuthConfig.Secret)),
                        ValidIssuer = config.AuthConfig.Issuer,
                        ValidAudience = config.AuthConfig.Audience,
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });
            services.AddHealthChecks();

            services.AddControllers();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var workerId = Environment.GetEnvironmentVariable("BKL_WORKER_ID");
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                var config = serviceScope.ServiceProvider.GetRequiredService<BklConfig>();

                if (Configuration.GetValue<string>("run") == "initDatabase")
                {
                    var context = serviceScope.ServiceProvider.GetRequiredService<BklDbContext>();
                    using (context)
                    {
                        context.Database.EnsureDeleted();
                        context.Database.Migrate();
                    }
                }
            }


            if (env.IsDevelopment())
            {
                Console.WriteLine("Developement Env ");
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseCors("cors");
            app.UseHealthChecks("/healthchecks");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                //endpoints.MapHub<PushStateHub>("/notitionhub");
                //endpoints.MapHub<PushAlarmHub>("/analysishub");
            });
        }
    }
}