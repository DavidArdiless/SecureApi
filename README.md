# 🔐 SecureApi

API REST en ASP.NET Core 8 para gestión de proveedores, con autenticación JWT propia, rate limiting y validación de datos — **sin una sola dependencia externa de NuGet** en el proyecto principal.

> Recreación en portfolio de un sistema real: la gestión de proveedores que desarrollé para el Ministerio de Educación de Tucumán, con foco puesto en seguridad.

## 📋 Por qué existe este proyecto

Quería un proyecto que mostrara tres cosas al mismo tiempo: que sé construir una API backend completa, que entiendo cómo funciona la seguridad por debajo (no solo "agregué el paquete de auth"), y que sé testear lo que escribo. De ahí las tres decisiones de diseño más importantes:

### 1. JWT implementado a mano (`Security/JwtService.cs`)

En vez de `Microsoft.AspNetCore.Authentication.JwtBearer`, escribí la generación y validación del token con `HMACSHA256` puro (`System.Security.Cryptography`). Esto es a propósito: quería entender —y poder explicar en una entrevista— exactamente cómo se arma un JWT (header.payload.signature en Base64Url) y qué protecciones hacen falta.

**Aclaración importante: no implementé criptografía.** El HMAC-SHA256 y la comparación en tiempo constante salen del framework. Lo que está hecho a mano es el *formato* del token y las *reglas de validación*, que es justo donde se cometen los errores explotables:

| Error clásico | Cómo lo cubre `JwtService.cs` |
|---|---|
| Comparar la firma con `==` (timing attack) | `CryptographicOperations.FixedTimeEquals` |
| Leer el payload antes de validar la firma | La firma se verifica primero; el payload se parsea después |
| Confiar en el `alg` del header (`none`, confusión de algoritmos) | HS256 está fijo en una constante del código, y además se rechaza cualquier `alg` distinto |
| No validar la ventana de validez | Se exige `exp` y se respeta `nbf` |
| Generar `iss` y no validarlo | Se compara contra el emisor configurado |
| Claim faltante que revienta el validador | Todos los claims se leen con `TryGetValue`; un token incompleto da 401, no 500 |

Los tests de `JwtServiceTests` mandan exactamente esos ataques (`alg: none`, `alg: RS256` con firma HS256 válida, emisor distinto, claims faltantes, `exp` no numérico) y verifican que todos se rechacen.

**Para producción real, la recomendación sigue siendo usar la librería estándar** (`System.IdentityModel.Tokens.Jwt` o `Microsoft.AspNetCore.Authentication.JwtBearer`): está más auditada y cubre cosas que esta implementación no tiene (rotación de claves, JWKS, `aud`, revocación).

### 2. Persistencia en memoria, no EF Core

El repositorio (`Data/ProveedorStore.cs`) guarda todo en un `ConcurrentDictionary`. La razón práctica: así cualquiera puede clonar el repo y correrlo sin instalar ni configurar una base de datos. La razón de diseño: la lógica de negocio no sabe ni le importa cómo se persisten los datos, así que cambiar esto por EF Core + SQL Server (que es justamente lo que usé en el sistema real del Ministerio) es un cambio acotado a esa clase, sin tocar los endpoints.

### 3. Contraseñas con PBKDF2 hecho a mano

`Security/PasswordHasher.cs` usa `Rfc2898DeriveBytes.Pbkdf2` con 210.000 iteraciones (la recomendación vigente de OWASP), salt aleatoria por usuario, y comparación en tiempo constante. Nunca se guarda ni se loguea una contraseña en texto plano.

## 🛡️ Seguridad implementada

- Autenticación JWT (HS256) en todos los endpoints de `/api/proveedores`, con validación de firma, `alg`, `iss`, `exp` y `nbf`.
- Contraseñas hasheadas con PBKDF2-SHA256 (210k iteraciones) + salt aleatoria, y comparación en tiempo constante.
- Rate limiting: 100 req/min por IP en general, 5 req/min por IP en `/api/auth/login` (mitiga fuerza bruta).
- Validación de entrada con Data Annotations en todos los POST/PUT (formato de CUIT, email, largos máximos).
- **Login sin oráculo de enumeración de usuarios**: el error es genérico *y* el tiempo de respuesta también. Si el usuario no existe, igual se corre PBKDF2 contra un hash señuelo. Sin eso, la diferencia medida era ~1,5 ms contra ~22 ms — suficiente para saber qué usuarios existen aunque el mensaje sea idéntico.
- **La app no arranca insegura**: fuera de `Development`, si falta `Jwt:Secret` o `Seed:AdminPassword`, el arranque falla con un error explícito en vez de caer en un valor por defecto. Un secreto por defecto en un repo público significa que cualquiera puede firmar tokens de admin.
- Contenedor Docker corriendo como usuario sin privilegios (no root).

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

Credenciales de desarrollo (seedeadas al arrancar, definidas en `appsettings.Development.json`): `admin` / `CambiarEsta123!`. Solo sirven en el entorno `Development`: en cualquier otro entorno la app exige las variables de entorno y no arranca sin ellas.

## ⚙️ Configuración en producción

Dos variables son obligatorias fuera de `Development`:

| Variable | Para qué |
|---|---|
| `SecureApi__Jwt__Secret` | Clave HMAC del JWT. Mínimo 32 caracteres. |
| `SecureApi__Seed__AdminPassword` | Contraseña del admin seedeado. |

El prefijo `SecureApi__` se registra explícitamente en `Program.cs`:

```csharp
builder.Configuration.AddEnvironmentVariables(prefix: "SecureApi__");
```

Vale la pena el detalle porque es un error de configuración fácil de no ver: el provider de variables de entorno que registra `CreateBuilder` **no usa ningún prefijo**. Sin esa línea, una variable `SecureApi__Jwt__Secret` se mapea a la clave `SecureApi:Jwt:Secret`, que nadie lee — la app no falla, simplemente ignora la variable y se queda con el valor de desarrollo. También funcionan los nombres sin prefijo (`Jwt__Secret`, `Seed__AdminPassword`).

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

40 tests (`SecureApi.Tests/`, xUnit):
- `JwtServiceTests` — generación y validación de tokens, más los ataques clásicos: `alg: none`, `alg: RS256` con firma HS256 válida, firma alterada, token vencido, `nbf` en el futuro, emisor distinto, claims obligatorios faltantes y `exp` no numérico.
- `UsuarioStoreTests` — que la app no arranque sin contraseña configurada fuera de `Development`, y que el login tarde lo mismo exista el usuario o no (el test compara ambos caminos, así que impide volver a introducir el oráculo de enumeración).
- `PasswordHasherTests` — hash y verificación de contraseñas.
- `ProveedorStoreTests` — altas, bajas y modificaciones, incluido que un `PUT` no pise `FechaAlta` ni reactive a un proveedor dado de baja.
- `ValidationTests` — formato de CUIT, email y campos obligatorios.

## 🐳 Docker

```bash
docker build -t secureapi -f SecureApi/Dockerfile ./SecureApi
docker run -p 8080:8080 \
  -e SecureApi__Jwt__Secret="una-clave-de-al-menos-32-caracteres" \
  -e SecureApi__Seed__AdminPassword="una-contrasena-de-admin" \
  secureapi
```

Si omitís alguna de las dos variables el contenedor arranca y falla a propósito, con el mensaje de qué falta.

## 🔧 CI/CD

- `.github/workflows/ci.yml` — `restore`, `build` y `test` de `SecureApi.sln` en cada push/PR, y valida que la imagen Docker compile.
- `.github/workflows/security.yml` — lo mismo más dos escaneos: **Gitleaks** sobre el historial completo (secretos commiteados) y **Trivy** sobre la imagen (CVEs `HIGH`/`CRITICAL`, con `exit-code: 1` para que el pipeline corte).

Gitleaks corre con la versión fijada (`8.30.1`) y el checksum verificado, en lugar de `gitleaks-action@v2`: el action trae un binario con un ruleset viejo que marcaba como filtración los valores de desarrollo que están commiteados a propósito. Fijar la versión deja el escaneo limpio sin suprimir hallazgos, y permite reproducir en local exactamente lo que corre en CI:

```bash
gitleaks detect --source . --config .gitleaks.toml --redact
```

Los dos declaran `permissions: contents: read` y construyen explícitamente `SecureApi/Dockerfile`, para que Trivy escanee la imagen de la API y no otra.

## 📌 Próximas mejoras (a propósito, no implementadas todavía)

- Migrar `ProveedorStore` de memoria a EF Core + SQL Server/PostgreSQL.
- Reemplazar el JWT propio por `Microsoft.AspNetCore.Authentication.JwtBearer` en un branch aparte, para comparar ambos enfoques.
- Refresh tokens (hoy el token expira a las 2 horas y no hay forma de renovarlo sin loguearse de nuevo), y revocación vía `jti`.
- Autorización por rol: el claim `role` se emite y se valida, pero todavía ningún endpoint exige `Admin`. Con un solo usuario en el sistema sería decorativo; con más de un rol, el `DELETE` debería pedirlo.
- `UseForwardedHeaders` para que el rate limiting particione por la IP real y no por la del proxy.
- Claim `aud` (hoy no se emite ni se valida, porque hay un solo consumidor).
- Paginación en `GET /api/proveedores`.

---

**Propósito educativo / portfolio.** [MIT License](LICENSE).
