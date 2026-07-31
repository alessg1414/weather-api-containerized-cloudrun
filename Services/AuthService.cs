using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataVisionAPI.Models.DTOs;

namespace DataVisionAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public AuthService(IUserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
        {
            try
            {
                // Buscar usuario usando el servicio
                var usuario = await _userService.GetUserByUsernameAsync(loginDto.Usuario);

                if (usuario == null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Usuario no encontrado"
                    };
                }

                // Verificar contraseña usando el servicio de hash seguro
                var isValidPassword = await _userService.ValidatePasswordAsync(loginDto.Password, usuario.Password);
                if (!isValidPassword)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Contraseña incorrecta"
                    };
                }

                // Generar token JWT
                var token = GenerateJwtToken(usuario.Usuario_, usuario.Rol, usuario.Id);

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Login exitoso",
                    Token = token,
                    Usuario = usuario.Usuario_,
                    Rol = usuario.Rol
                };
            }
            catch (Exception ex)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = $"Error en el login: {ex.Message}"
                };
            }
        }

        public string GenerateJwtToken(string usuario, string rol, int userId)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] 
                ?? throw new InvalidOperationException("JwtSettings:SecretKey no está configurada.");

            // CAMBIO CLAVE: Usar UTF8 para ser 100% consistente con Program.cs
            var key = Encoding.UTF8.GetBytes(secretKey);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, usuario),
                new Claim(ClaimTypes.Role, rol),
                new Claim("UserId", userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, usuario),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var expiryInHours = double.TryParse(jwtSettings["ExpiryInHours"], out var hours) ? hours : 24;

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(expiryInHours),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}