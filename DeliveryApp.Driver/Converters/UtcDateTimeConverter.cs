using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeliveryApp.Driver.Converters
{
    /// <summary>
    /// ✅ Converter بيتسجل مرة واحدة في JsonSerializerOptions المشتركة (ApiService._json)
    /// وبيشتغل تلقائي على أي DateTime جاي من الـ API.
    ///
    /// الـ API بيبعت التواريخ UTC (وبقت معلّمة بـ "Z" في الآخر بعد الفيكس اللي
    /// حصل في السيرفر). المشكلة كانت إن كل شاشة كانت بتعرض القيمة UTC زي ما
    /// هي (CreatedAt.ToString(...)) من غير تحويل، فالمستخدم بيشوف وقت غلط.
    ///
    /// الحل هنا: بمجرد ما القيمة توصل للتطبيق وتتحول من JSON لـ DateTime، بنحوّلها
    /// فورًا لتوقيت الجهاز المحلي (اللي هو توقيت مصر عند المستخدم). بعد كده أي
    /// Binding أو ToString() في أي شاشة هيشتغل صح تلقائي من غير ما نلمس كل شاشة.
    /// </summary>
    public class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime(); // System.Text.Json بيرجعها Kind=Utc لو فيها "Z"
            var utc = value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return utc.ToLocalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // بنبعت للسيرفر بتوقيت UTC زي ما هو متوقع منه
            writer.WriteStringValue(value.ToUniversalTime());
        }
    }

    public class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var value = reader.GetDateTime();
            var utc = value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return utc.ToLocalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToUniversalTime());
            else
                writer.WriteNullValue();
        }
    }
}
