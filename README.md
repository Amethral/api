# Amethral API - Coolify Deployment Guide

This guide will help you deploy the Amethral API to Coolify with automatic database migrations.

## 📋 Prerequisites

- Coolify instance running
- Git repository connected to Coolify
- Domain name (optional, but recommended)

## 🚀 Deployment Steps

### 1. Create a New Service in Coolify

1. Log into your Coolify dashboard
2. Create a new **Docker Compose** service
3. Connect your Git repository
4. Set the branch you want to deploy (e.g., `main`)

### 2. Configure Environment Variables

In Coolify, add the following environment variables:

#### Required Variables

```env
# Database
POSTGRES_PASSWORD=your_secure_database_password

# JWT (CRITICAL - Generate a strong secret key)
JWT_SECRET_KEY=your_very_long_and_secure_secret_key_minimum_32_characters

# Frontend URL (for CORS)
FRONTEND_URL=https://your-frontend-domain.com
```

#### Optional Variables

```env
# Database (if you want to customize)
POSTGRES_USER=postgres
POSTGRES_DB=mmorpg_auth

# API Port (default: 8080)
API_PORT=8080

# Environment
ASPNETCORE_ENVIRONMENT=Production

# JWT Customization
JWT_ISSUER=Amethral
JWT_AUDIENCE=AmethralAPI

# OAuth - Google
GOOGLE_CLIENT_ID=your_google_client_id
GOOGLE_CLIENT_SECRET=your_google_client_secret

# OAuth - Discord
DISCORD_CLIENT_ID=your_discord_client_id
DISCORD_CLIENT_SECRET=your_discord_client_secret
```

### 3. Generate a Secure JWT Secret

Use one of these methods to generate a secure JWT secret:

```bash
# Method 1: Using OpenSSL
openssl rand -base64 64

# Method 2: Using Node.js
node -e "console.log(require('crypto').randomBytes(64).toString('base64'))"

# Method 3: Using Python
python3 -c "import secrets; print(secrets.token_urlsafe(64))"
```

### 4. Configure Domain (Optional)

1. In Coolify, go to your service settings
2. Add your domain name
3. Enable SSL/TLS (Let's Encrypt)
4. The API will be available at: `https://your-domain.com`

### 5. Deploy

1. Click **Deploy** in Coolify
2. Monitor the build logs
3. The deployment process will:
   - Build the Docker image
   - Start the PostgreSQL database
   - Wait for the database to be ready
   - **Automatically run EF Core migrations**
   - Start the API

## 🔍 Verify Deployment

### Check API Health

Visit your API endpoint:

```
https://your-domain.com/swagger
```

You should see the Swagger UI with all available endpoints.

### Check Logs

In Coolify, view the logs to ensure:

- Database connection is successful
- Migrations ran without errors
- API started successfully

Look for these messages:

```
Database is ready!
Running EF Core migrations...
Migrations completed successfully!
Starting application...
```

## 🗄️ Database Migrations

### Automatic Migrations

The deployment is configured to **automatically run migrations** on every deployment. The `entrypoint.sh` script:

1. Waits for PostgreSQL to be ready
2. Runs `dotnet ef database update`
3. Starts the API

### Manual Migration (if needed)

If you need to run migrations manually:

```bash
# Connect to the API container
docker exec -it amethral-api bash

# Run migrations
cd /app/src
dotnet ef database update
```

### Create New Migrations (Development)

When developing locally and you need to create a new migration:

```bash
# Navigate to the API project
cd Amethral.Api

# Create a new migration
dotnet ef migrations add YourMigrationName

# Apply the migration locally
dotnet ef database update
```

Then commit and push. Coolify will automatically apply the new migration on deployment.

## 🔧 Troubleshooting

### Database Connection Issues

If the API can't connect to the database:

1. Check the `POSTGRES_PASSWORD` environment variable
2. Verify the database container is running: `docker ps`
3. Check database logs in Coolify

### Migration Errors

If migrations fail:

1. Check the migration logs in Coolify
2. Verify all migration files are committed to Git
3. Ensure the database user has proper permissions

### API Not Starting

1. Check the container logs in Coolify
2. Verify all required environment variables are set
3. Ensure the `JWT_SECRET_KEY` is set and valid

## 🔐 Security Recommendations

1. **Use strong passwords** for `POSTGRES_PASSWORD`
2. **Generate a cryptographically secure** `JWT_SECRET_KEY` (minimum 32 characters)
3. **Enable HTTPS** in Coolify with Let's Encrypt
4. **Set proper CORS origins** in `FRONTEND_URL`
5. **Never commit** `.env` files to Git
6. **Rotate secrets** regularly

## 📊 Monitoring

### Health Checks

The PostgreSQL service includes a health check:

- Runs every 10 seconds
- Ensures the database is ready before starting the API

### Logs

Monitor your application logs in Coolify:

- Database connection status
- Migration execution
- API requests and errors
- Authentication events

## 🔄 Updates and Redeployment

To update your API:

1. Push changes to your Git repository
2. Coolify will automatically rebuild and redeploy
3. Migrations will run automatically
4. Zero-downtime deployment (if configured)

## 📝 Environment Variables Reference

| Variable                 | Required | Default       | Description             |
| ------------------------ | -------- | ------------- | ----------------------- |
| `POSTGRES_USER`          | No       | `postgres`    | Database username       |
| `POSTGRES_PASSWORD`      | **Yes**  | -             | Database password       |
| `POSTGRES_DB`            | No       | `mmorpg_auth` | Database name           |
| `API_PORT`               | No       | `8080`        | API port                |
| `ASPNETCORE_ENVIRONMENT` | No       | `Production`  | ASP.NET environment     |
| `FRONTEND_URL`           | **Yes**  | -             | Frontend URL for CORS   |
| `JWT_SECRET_KEY`         | **Yes**  | -             | JWT signing key         |
| `JWT_ISSUER`             | No       | `Amethral`    | JWT issuer              |
| `JWT_AUDIENCE`           | No       | `AmethralAPI` | JWT audience            |
| `GOOGLE_CLIENT_ID`       | No       | -             | Google OAuth client ID  |
| `GOOGLE_CLIENT_SECRET`   | No       | -             | Google OAuth secret     |
| `DISCORD_CLIENT_ID`      | No       | -             | Discord OAuth client ID |
| `DISCORD_CLIENT_SECRET`  | No       | -             | Discord OAuth secret    |

## 🆘 Support

If you encounter issues:

1. Check Coolify logs
2. Review the troubleshooting section
3. Verify environment variables
4. Check database connectivity

## 📚 Additional Resources

- [Coolify Documentation](https://coolify.io/docs)
- [ASP.NET Core Deployment](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/)
- [Entity Framework Core Migrations](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
