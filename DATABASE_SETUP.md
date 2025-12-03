# Database Deployment Guide

## Deploying the Database in Coolify

This guide shows you how to deploy the PostgreSQL database as a separate service in Coolify.

## 📋 Steps

### 1. Create a New Service for the Database

1. In Coolify, create a **new Docker Compose service**
2. Name it something like `amethral-database`
3. Connect to the same Git repository
4. Set the **Compose File Path** to: `docker-compose.db.yml`

### 2. Set Environment Variables

In Coolify, add these environment variables for the database service:

#### Required:

```env
POSTGRES_PASSWORD=your_very_secure_database_password
```

#### Optional (with defaults):

```env
POSTGRES_USER=postgres
POSTGRES_DB=mmorpg_auth
POSTGRES_PORT=5432
```

### 3. Deploy the Database

Click **Deploy** in Coolify. The database will:

- Create a PostgreSQL 15 container
- Set up the database with your credentials
- Create a persistent volume for data
- Run health checks

### 4. Get the Database Connection Info

After deployment, note the following for your API configuration:

- **Host**: The service name in Coolify (e.g., `amethral-postgres` or the container name)
- **Port**: `5432` (default)
- **Database**: `mmorpg_auth` (or your custom name)
- **Username**: `postgres` (or your custom user)
- **Password**: The password you set in environment variables

### 5. Configure the API

In your API service (the main `docker-compose.yml`), set the `DATABASE_URL` environment variable:

```env
DATABASE_URL=Host=amethral-postgres;Port=5432;Database=mmorpg_auth;Username=postgres;Password=your_password
```

**Important:** The `Host` should be the **container name** of your database service. In Coolify, services on the same network can communicate using container names.

## 🔍 Finding the Database Hostname

### Option 1: Use Container Name

The container name is set in `docker-compose.db.yml` as `amethral-postgres`. Use this as the host:

```
Host=amethral-postgres
```

### Option 2: Use Service Name

If Coolify uses the service name, it might be something like:

```
Host=amethral-database
```

### Option 3: Check Coolify

1. Go to your database service in Coolify
2. Look for the internal hostname or container name
3. Use that in your connection string

## 🧪 Testing the Connection

### From the API Container

Once both services are deployed, you can test the connection:

1. Go to your API service in Coolify
2. Open the terminal/console
3. Run:

```bash
# Test if you can reach the database
nc -zv amethral-postgres 5432

# Or use psql if available
psql -h amethral-postgres -U postgres -d mmorpg_auth
```

## 📊 Database Management

### Accessing the Database

You can connect to your database using:

**From Coolify Terminal:**

```bash
docker exec -it amethral-postgres psql -U postgres -d mmorpg_auth
```

**From External Tool (pgAdmin, DBeaver, etc.):**

- Host: Your Coolify server IP or domain
- Port: The exposed port (default: 5432)
- Database: mmorpg_auth
- Username: postgres
- Password: Your password

### Backup the Database

```bash
# Create a backup
docker exec amethral-postgres pg_dump -U postgres mmorpg_auth > backup.sql

# Restore from backup
docker exec -i amethral-postgres psql -U postgres mmorpg_auth < backup.sql
```

## 🔒 Security Notes

- ✅ Use a **strong password** for `POSTGRES_PASSWORD`
- ✅ Don't expose the database port publicly if not needed
- ✅ Keep the database on the same Coolify network as the API
- ✅ Regular backups are recommended
- ⚠️ Never commit `.env` files with real passwords to Git

## 🔄 Updating the Database

The database service will persist data in a Docker volume. When you redeploy:

- ✅ Data is preserved
- ✅ Configuration updates are applied
- ✅ No data loss (unless you delete the volume)

## 📝 Summary

**Two Separate Services in Coolify:**

1. **Database Service** (`docker-compose.db.yml`)

   - Runs PostgreSQL
   - Has its own environment variables
   - Persists data in a volume

2. **API Service** (`docker-compose.yml`)
   - Runs your .NET API
   - Connects to the database using `DATABASE_URL`
   - Runs migrations automatically

Both services communicate over Coolify's network using container names! 🚀
