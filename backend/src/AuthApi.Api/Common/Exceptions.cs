namespace AuthApi.Api.Common;

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class UnauthorizedDomainException(string message) : Exception(message);
