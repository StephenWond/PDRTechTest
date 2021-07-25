using System;
using PDR.PatientBooking.Service.BookingServices.Requests;
using PDR.PatientBooking.Service.BookingServices.Responses;

namespace PDR.PatientBooking.Service.BookingServices
{
    public interface IBookingService
    {
        GetBookingResponse GetNextBooking(long patientId);
        void AddBooking(AddBookingRequest request);
        void DeleteBooking(Guid bookingId);
    }
}