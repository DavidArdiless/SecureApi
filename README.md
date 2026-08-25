# 🔐 SecureApi

API REST en ASP.NET Core 8 para gestión de proveedores, con autenticación JWT propia, rate limiting y validación de datos — **sin una sola dependencia externa de NuGet** en el proyecto principal.

> Recreación en portfolio de un sistema real: la gestión de proveedores que desarrollé para el Ministerio de Educación de Tucumán, con foco puesto en seguridad.

## 📋 Por qué existe este proyecto

Quería un proyecto que mostrara tres cosas al mismo tiempo: que sé construir una API backend completa, que entiendo cómo funciona la seguridad por debajo (no solo "agregué el paquete de auth"), y que sé testear lo que escribo. De ahí las tres decisiones de diseño más importantes:

### 1. JWT implementado a mano (`Security/JwtService.cs`)

En vez de `Microsoft.AspNetCore.Authentication.JwtBearer`, escribí la generación y validación del token con `HMACSHA256` puro (`System.Security.Cryptography`). Esto es a propósito: quería entender —y poder explicar en una entrevista— exactamente cómo se arma un JWT (header.payload.signature en Base64Url) y qué protecciones hacen falta (comparación de firma en tiempo constante con `CryptographicOperations.FixedTimeEquals`, validación de expiración, rechazo si falta la firma).

**Para producción real, la recomendación es usar la librería estándar** (`System.IdentityModel.Tokens.Jwt` o `Microsoft.AspNetCore.Authentication.JwtBearer`), que está más auditada y cubre casos borde que esta implementación no cubre (rotación de claves, múltiples algoritmos, etc.).

### 2. Persistencia en memoria, no EF Core

El repositorio (`Data/ProveedorStore.cs`) guarda todo en un `ConcurrentDictionary`. La razón práctica: así cualquiera puede clonar el repo y correrlo sin instalar ni configurar una base de datos. La razón de diseño: la lógica de negocio no sabe ni le importa cómo se persisten los datos, así que cambiar esto por EF Core + SQL Server (que es justamente lo que usé en el sistema real del Ministerio) es un cambio acotado a esa clase, sin tocar los endpoints.

### 3. Contraseñas con PBKDF2 hecho a mano

`Security/PasswordHasher.cs` usa `Rfc2898DeriveBytes.Pbkdf2` con 210.000 iteraciones (la recomendación vigente de OWASP), salt aleatoria por usuario, y comparación en tiempo constante. Nunca se guarda ni se loguea una contraseña en texto plano.

## 🛡️ Seguridad implementada

- Autenticación JWT (HS256) en todos los endpoints de `/api/proveedores`.
- Contraseñas hasheadas con PBKDF2-SHA256 (210k iteraciones) + salt aleatoria.
- Rate limiting: 100 req/min por IP en general, 5 req/min por IP en `/api/auth/login` (mitiga fuerza bruta).
- Validación de entrada con Data Annotations en todos los POST/PUT (incluye formato de CUIT, email, longitud de campos).
- Mensajes de error de login genéricos (no revelan si falló el usuario o la contraseña).
- Contenedor Docker corriendo como usuario sin privilegios (no root).
- Sin secretos hardcodeados: `Jwt:Secret` y `Seed:AdminPassword` se leen de configuración, con defaults de *solo desarrollo* claramente marcados.

## 🗂️ Endpoints

| Método | Ruta                     | Auth | Descripción                        |
|--------|--------------------------|------|-------------------------------------|
| POST   | `/api/auth/login`        | No   | Devuelve un JWT (rate limit: 5/min) |
| GET    | `/api/proveedores`       | Sí   | Lista todos los proveedores         |
| GET    | `/api/proveedores/{id}`  | Sí   | Obtiene un proveedor por id         |
| POST   | `/api/proveedores`       | Sí   | Crea un proveedor                   |
| PUT    | `/api/proveedores/{id}`  | Sí   | Actualiza un proveedor              |
| DELETE | `/api/proveedores/{id}`  | Sí   | Elimina un proveedor                |
| GET    | `/health`                | No   | Healthcheck                         |

Credenciales de desarrollo (seedeadas al arrancar, solo en `appsettings.Development.json`): `admin` / `CambiarEsta123!`.

## 🚀 Correrlo localmente

Requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/DavidArdiless/SecureApi
cd SecureApi/SecureApi
dotnet run
```

Ejemplo de uso con curl:

```bash
# Login
TOKEN=$(curl -s -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"nombreUsuario":"admin","password":"CambiarEsta123!"}' | jq -r .token)

# Listar proveedores (requiere el token)
curl http://localhost:5080/api/proveedores -H "Authorization: Bearer $TOKEN"
```

## ✅ Tests

```bash
dotnet test
```

Los tests (`SecureApi.Tests/`, xUnit) cubren:
- `PasswordHasherTests` — hash y verificación de contraseñas.
- `JwtServiceTests` — generación de tokens, rechazo de tokens alterados o vencidos.
- `ProveedorStoreTests` — altas, bajas, y modificaciones sobre el repositorio.
- `ValidationTests` — formato de CUIT, email y campos obligatorios.

## 🐳 Docker

```bash
docker build -t secureapi -f SecureApi/Dockerfile ./SecureApi
docker run -p 8080:8080 -e SecureApi__Jwt__Secret="una-clave-de-al-menos-32-caracteres" secureapi
```

## 🔧 CI/CD

`.github/workflows/ci.yml` corre `dotnet restore`, `build` y `test` en cada push/PR, y valida que la imagen Docker compile.

## 📌 Próximas mejoras (a propósito, no implementadas todavía)

- Migrar `ProveedorStore` de memoria a EF Core + SQL Server/PostgreSQL.
- Reemplazar el JWT propio por `Microsoft.AspNetCore.Authentication.JwtBearer` en un branch aparte, para comparar ambos enfoques.
- Refresh tokens (hoy el token expira a las 2 horas y no hay forma de renovarlo sin loguearse de nuevo).
- Paginación en `GET /api/proveedores`.

---

**Propósito educativo / portfolio.** MIT License.
