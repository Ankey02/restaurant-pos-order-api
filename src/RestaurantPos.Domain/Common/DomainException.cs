namespace RestaurantPos.Domain.Common;

// Thrown when a business rule is broken. The API turns this into a 400 or 409 response.
public class DomainException(string message) : Exception(message);
