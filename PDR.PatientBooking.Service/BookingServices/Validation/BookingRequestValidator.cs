using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;
using PDR.PatientBooking.Data;
using PDR.PatientBooking.Service.BookingServices.Requests;
using PDR.PatientBooking.Service.Validation;

namespace PDR.PatientBooking.Service.BookingServices.Validation
{
    public class BookingRequestValidator : IBookingRequestValidator
    {
        private readonly PatientBookingContext _context;
        private readonly ISystemClock _systemClock;

        public BookingRequestValidator(PatientBookingContext context, ISystemClock systemClock)
        {
            _context = context;
            _systemClock = systemClock;
        }

        public PdrValidationResult ValidateRequest(AddBookingRequest request)
        {
            var result = new PdrValidationResult(true);

            if (PatientNotFound(request.PatientId, ref result))
                return result;

            if (DoctorNotFound(request.DoctorId, ref result))
                return result;

            if (InvalidBookingDateTime(request, ref result))
                return result;

            if (DoctorNotAvailable(request, ref result))
                return result;

            return result;
        }

        public PdrValidationResult ValidateRequest(long patientId)
        {
            var result = new PdrValidationResult(true);

            if (PatientNotFound(patientId, ref result))
                return result;

            return result;
        }

        public PdrValidationResult ValidateRequest(Guid bookingId)
        {
            var result = new PdrValidationResult(true);

            if (BookingNotFound(bookingId, ref result))
                return result;

            return result;
        }

        private bool PatientNotFound(long patientId, ref PdrValidationResult result)
        {
            if (!_context.Patient.Any(x => x.Id == patientId))
            {
                result.PassedValidation = false;
                result.Errors.Add("A patient with that ID could not be found");
                return true;
            }

            return false;
        }

        private bool DoctorNotFound(long doctorId, ref PdrValidationResult result)
        {
            if (!_context.Doctor.Any(x => x.Id == doctorId))
            {
                result.PassedValidation = false;
                result.Errors.Add("A doctor with that ID could not be found");
                return true;
            }

            return false;
        }

        private bool InvalidBookingDateTime(AddBookingRequest request, ref PdrValidationResult result)
        {
            var errors = new List<string>();

            if (request.StartTime < _systemClock.UtcNow.UtcDateTime)
                errors.Add("An appointment cannot be booked in the past");

            if (request.StartTime >= request.EndTime)
                errors.Add("An appointment end time must be after the start time");

            if (errors.Any())
            {
                result.PassedValidation = false;
                result.Errors.AddRange(errors);
                return true;
            }

            return false;
        }

        private bool DoctorNotAvailable(AddBookingRequest request, ref PdrValidationResult result)
        {
            var doctor = _context.Doctor.FirstOrDefault(x => x.Id == request.DoctorId);

            if (doctor.Orders.Any(x => request.StartTime <= x.EndTime && request.EndTime >= x.StartTime))
            {
                result.PassedValidation = false;
                result.Errors.Add("The requested appointment time with this doctor is not available");
                return true;
            }

            return false;
        }

        private bool BookingNotFound(Guid bookingId, ref PdrValidationResult result)
        {
            if (!_context.Order.Any(x => x.Id == bookingId))
            {
                result.PassedValidation = false;
                result.Errors.Add("A booking with that ID could not be found");
                return true;
            }

            return false;
        }
    }
}