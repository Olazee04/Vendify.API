using Vendify.Core.Entities;
using Vendify.Core.Enums;

namespace Vendify.Infrastructure.Services.Implementations
{
    // One shared set of shipping rules, used by the cart,
    // orders and stockpile checkout.
    public static class ShippingZoneMatcher
    {
        // The fee a customer pays for this zone
        public static decimal FeeOf(ShippingZone z) =>
            z.Type == ShippingType.FreeShipping ||
            z.Type == ShippingType.Pickup
                ? 0m
                : z.Fee;

        // Does this zone serve the given state (and country)?
        // A zone can list several states, separated by commas.
        public static bool Covers(
            ShippingZone z, string? state, string? country = "Nigeria")
        {
            var isNigeria = string.Equals(
                country ?? "Nigeria", "Nigeria",
                StringComparison.OrdinalIgnoreCase);
            var name = Normalize(z.Name);

            if (!isNigeria) return name == "international";
            if (name == "international") return false;

            var states = ZoneStates(z);
            if (states.Count > 0)
                return states.Contains(Normalize(state));

            // No states chosen: a Nationwide zone serves everywhere
            if (IsNationwide(z)) return true;

            // Older zones were simply named after their state
            return name == Normalize(state);
        }

        // A zone the customer picked from the list
        public static ShippingZone? FindById(
            IEnumerable<ShippingZone> zones,
            Guid zoneId,
            string? state,
            string? country = "Nigeria")
        {
            var zone = zones.FirstOrDefault(z =>
                z.Id == zoneId && z.IsActive);

            return zone != null && Covers(zone, state, country)
                ? zone
                : null;
        }

        // The older automatic rule: city, then state, then Nationwide.
        // Store pickup is never chosen automatically.
        public static ShippingZone? FindZone(
            IEnumerable<ShippingZone> zones,
            string? state,
            string? country = "Nigeria",
            string? city = null)
        {
            var active = zones
                .Where(z => z.IsActive && z.Type != ShippingType.Pickup)
                .ToList();

            if (!string.Equals(country ?? "Nigeria", "Nigeria",
                    StringComparison.OrdinalIgnoreCase))
            {
                return active.FirstOrDefault(z =>
                    Normalize(z.Name) == "international");
            }

            var wantedCity = Normalize(city);

            if (wantedCity != "")
            {
                var cityMatch = active.FirstOrDefault(z =>
                    !string.IsNullOrWhiteSpace(z.City) &&
                    Normalize(z.City) == wantedCity &&
                    Covers(z, state, country));
                if (cityMatch != null) return cityMatch;
            }

            var stateMatch = active.FirstOrDefault(z =>
                string.IsNullOrWhiteSpace(z.City) &&
                !IsNationwide(z) &&
                Covers(z, state, country));
            if (stateMatch != null) return stateMatch;

            return active.FirstOrDefault(IsNationwide);
        }

        private static List<string> ZoneStates(ShippingZone z) =>
            (z.State ?? "")
                .Split(',')
                .Select(Normalize)
                .Where(s => s != "")
                .ToList();

        private static bool IsNationwide(ShippingZone z)
        {
            var n = Normalize(z.Name);
            return n == "nationwide" || n == "all states" || n == "all";
        }

        private static string Normalize(string? value)
        {
            var v = (value ?? "").Trim().ToLower().Replace(" state", "");
            if (v == "fct" || v == "fct abuja" || v == "abuja")
                return "abuja";
            return v;
        }
    }
}