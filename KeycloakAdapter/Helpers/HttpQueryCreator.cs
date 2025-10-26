using System.Net;
using System.Reflection;

namespace KeycloakAdapter.Helpers
{
    public static class HttpQueryCreator
    {
        public static string BuildQueryForTenantSearchByDomain(
            bool? briefRepresentation = null,
            bool? exact = null,
            int? first = null,
            int? max = null,
            string? q = null,
            string? search = null)
        {
            return BuildQuery(new
            {
                briefRepresentation,
                exact,
                first,
                max,
                q,
                search
            });
        }

        public static string BuildQueryForTenantSearchByName(
            string? search,
            bool? exact = true,
            bool? briefRepresentation = null,
            int? first = null,
            int? max = null)
        {
            return BuildQuery(new
            {
                search,
                exact,
                briefRepresentation,
                first,
                max
            });
        }

        public static string BuildQueryForUserSearchByEmail(
            string? email,
            bool? exact = null,
            bool? briefRepresentation = null,
            int? first = null,
            int? max = null,
            bool? enabled = null,
            bool? emailVerified = null)
        {
            return BuildQuery(new
            {
                email,
                exact,
                briefRepresentation,
                enabled,
                emailVerified,
                first,
                max
            });
        }

        public static string BuildQueryForEvents(
            string? client = null,
            string? dateFrom = null,
            string? dateTo = null,
            int? first = null,
            string? ipAddress = null,
            int? max = null,
            string? type = null,
            string? user = null)
        {
            return BuildQuery(new
            {
                client,
                dateFrom,
                dateTo,
                first,
                ipAddress,
                max,
                type,
                user
            });
        }

        private static string BuildQuery(object parameters)
        {
            var dict = parameters
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => p.Name, p => p.GetValue(parameters, null));

            return ToQueryString(dict);
        }

        public static string ToQueryString(IDictionary<string, object?> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return string.Empty;

            var query = string.Join("&", parameters
                .Where(kv => kv.Value != null && !string.IsNullOrWhiteSpace(kv.Value.ToString()))
                .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value!.ToString())}"));

            return string.IsNullOrEmpty(query) ? string.Empty : $"?{query}";
        }
    }
}