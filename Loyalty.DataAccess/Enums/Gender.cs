namespace Loyalty.DataAccess.Enums
{
    /// <summary>
    /// Vahid cins kodu (2026-10-02): bütün klientlər (Flutter app, wallet qeydiyyat formu, admin web) 0=Kişi, 1=Qadın göndərir.
    /// Əvvəl enum 1/2/3 idi və PrimeHill importu həmin kodla yazmışdı — migration AddNotificationSchedulingAndBirthday datanı normallaşdırır.
    /// </summary>
    public enum Gender
    {
        Male = 0,
        Female = 1,
        Other = 2
    }
}
