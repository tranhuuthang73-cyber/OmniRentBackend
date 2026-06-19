namespace OmniRentBackend.Models.ViewModels
{
    public class AdminOwnerCommissionSummary
    {
        public string OwnerId { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public int BookingCount { get; set; }

        public double Revenue { get; set; }

        public double Commission { get; set; }

        public double UnpaidCommission { get; set; }

        public double PaidCommission { get; set; }
    }
}
