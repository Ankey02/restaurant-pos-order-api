namespace RestaurantPos.Application.Common;

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public sealed record RestaurantSettings(int TaxRateBasisPoints);
