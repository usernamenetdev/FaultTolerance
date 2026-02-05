namespace OrderService
{
    public static class Helpers
    {
        public static string RandomString(string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            if (string.IsNullOrEmpty(alphabet)) throw new ArgumentException("Alphabet is empty.", nameof(alphabet));

            var rng = Random.Shared;
            return new string(Enumerable.Range(0, 15)
                .Select(_ => alphabet[rng.Next(alphabet.Length)])
                .ToArray());
        }
    }
}
