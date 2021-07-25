using System;
using System.Linq;
using Microsoft.Extensions.Internal;
using PDR.PatientBooking.Data;
using PDR.PatientBooking.Data.Models;
using PDR.PatientBooking.Service.BookingServices.Requests;
using PDR.PatientBooking.Service.BookingServices.Responses;
using PDR.PatientBooking.Service.BookingServices.Validation;

namespace PDR.PatientBooking.Service.BookingServices
{
    public class BookingService : IBookingService
    {
        private readonly PatientBookingContext _context;
        private readonly IBookingRequestValidator _validator;
        private readonly ISystemClock _systemClock;

        public BookingService(
            PatientBookingContext context,
            IBookingRequestValidator validator,
            ISystemClock systemClock
        )
        {
            _context = context;
            _validator = validator;
            _systemClock = systemClock;
        }

        public GetBookingResponse GetNextBooking(long patientId)
        {
            var validationResult = _validator.ValidateRequest(patientId);

            if (!validationResult.PassedValidation)
            {
                throw new ArgumentException(validationResult.Errors.First());
            }

            var nextBooking =
                _context.Order
                    .Where(x =>
                        x.PatientId == patientId &&
                        x.StartTime > _systemClock.UtcNow.UtcDateTime &&
                        !x.IsDeleted)
                    .OrderBy(x => x.StartTime)
                    .FirstOrDefault();

            if (nextBooking != null)
            {
                return new GetBookingResponse
                {
                    Id = nextBooking.Id,
                    StartTime = nextBooking.StartTime,
                    EndTime = nextBooking.EndTime,
                    PatientId = nextBooking.PatientId,
                    DoctorId = nextBooking.DoctorId,
                    SurgeryType = (SurgeryType) nextBooking.SurgeryType
                };
            }

            return null;
        }

        public void AddBooking(AddBookingRequest request)
        {
            var validationResult = _validator.ValidateRequest(request);

            if (!validationResult.PassedValidation)
            {
                throw new ArgumentException(validationResult.Errors.First());
            }

            _context.Order.Add(new Order
                {
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    SurgeryType = (int) _context.Patient.FirstOrDefault(x => x.Id == request.PatientId).Clinic
                        .SurgeryType,
                    PatientId = request.PatientId,
                    DoctorId = request.DoctorId,
                    Patient = _context.Patient.FirstOrDefault(x => x.Id == request.PatientId),
                    Doctor = _context.Doctor.FirstOrDefault(x => x.Id == request.DoctorId),
                }
            );

            _context.SaveChanges();
        }

        public void DeleteBooking(Guid bookingId)
        {
            var validationResult = _validator.ValidateRequest(bookingId);

            if (!validationResult.PassedValidation)
            {
                throw new ArgumentException(validationResult.Errors.First());
            }

            var booking = _context.Order.FirstOrDefault(x => x.Id == bookingId);
            booking.IsDeleted = true;
            _context.SaveChanges();
        }
    }
}