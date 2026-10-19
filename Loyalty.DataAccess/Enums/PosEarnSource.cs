namespace Loyalty.DataAccess.Enums
{
    /// <summary>
    /// POS (iiko) inteqrasiyalı proqramda qazancı kim hesablayır.
    /// Legacy — bugünkü davranış: həm Walvero (OrderClosed), həm iiko cüzdan güzgüsü (iiko_webhook) işləyir.
    /// WalveroRules — Walvero hesablayır; iiko cüzdan güzgüsü balansa toxunmur (yalnız marker).
    /// PosMirror — iiko hesablayır (cüzdan güzgüsü balansı yazır); OrderClosed earn və cüzdan ödənişi keçilir.
    /// </summary>
    public enum PosEarnSource
    {
        Legacy = 0,
        WalveroRules = 1,
        PosMirror = 2
    }
}
