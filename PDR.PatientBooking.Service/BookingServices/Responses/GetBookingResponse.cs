using System;
using PDR.PatientBooking.Data.Models;

namespace PDR.PatientBooking.Service.BookingServices.Responses
{
    public class GetBookingResponse
    {
        public Guid Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long PatientId { get; set; }
        public long DoctorId { get; set; }
        public SurgeryType SurgeryType { get; set; }
    }
}