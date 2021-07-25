using System;
using System.Collections.Generic;
using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Internal;
using Moq;
using NUnit.Framework;
using PDR.PatientBooking.Data;
using PDR.PatientBooking.Data.Models;
using PDR.PatientBooking.Service.BookingServices.Requests;
using PDR.PatientBooking.Service.BookingServices.Validation;

namespace PDR.PatientBooking.Service.Tests.BookingServices.Validation
{
    [TestFixture]
    public class BookingRequestValidatorTests
    {
        private IFixture _fixture;

        private PatientBookingContext _context;

        private BookingRequestValidator _addBookingRequestValidator;

        private Mock<ISystemClock> _systemClock;

        [SetUp]
        public void SetUp()
        {
            // Boilerplate
            _fixture = new Fixture();

            //Prevent fixture from generating from entity circular references 
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior(1));

            // Mock setup
            _context = new PatientBookingContext(new DbContextOptionsBuilder<PatientBookingContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            _systemClock = new Mock<ISystemClock>();

            // Mock default
            SetupMockDefaults();

            // Sut instantiation
            _addBookingRequestValidator = new BookingRequestValidator(
                _context,
                _systemClock.Object
            );
        }

        private void SetupMockDefaults()
        {
            _systemClock
                .Setup(x => x.UtcNow)
                .Returns(DateTime.UtcNow);
        }

        [Test]
        public void ValidateRequestWithAddBookingRequest_AllChecksPass_ReturnsPassedValidationResult()
        {
            //arrange
            var request = GetBookingRequest();

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeTrue();
        }
        
        [Test]
        public void ValidateRequestWithAddBookingRequest_NoPatient_ReturnsFailedValidationResult()
        {
            //arrange
            var request = _fixture.Create<AddBookingRequest>();

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeFalse();
            res.Errors.Should().Contain("A patient with that ID could not be found");
        }
        
        [Test]
        public void ValidateRequestWithAddBookingRequest_NoDoctor_ReturnsFailedValidationResult()
        {
            //arrange
            var request = GetBookingRequest();
            request.DoctorId = 0;

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeFalse();
            res.Errors.Should().Contain("A doctor with that ID could not be found");
        }

        [Test]
        public void ValidateRequestWithAddBookingRequest_HistoricalStartTime_ReturnsFailedValidationResult()
        {
            //arrange
            var request = GetBookingRequest();
            request.StartTime = _systemClock.Object.UtcNow.UtcDateTime.AddMinutes(-1);

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeFalse();
            res.Errors.Should().Contain("An appointment cannot be booked in the past");
        }

        [TestCase(-1)]
        [TestCase(0)]
        public void ValidateRequestWithAddBookingRequest_EndBeforeStartTime_ReturnsFailedValidationResult(int minutes)
        {
            //arrange
            var request = GetBookingRequest();
            request.StartTime = _systemClock.Object.UtcNow.UtcDateTime.AddMinutes(1);
            request.EndTime = request.StartTime.AddMinutes(minutes);

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeFalse();
            res.Errors.Should().Contain("An appointment end time must be after the start time");
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(-1, -1)]
        public void ValidateRequestWithAddBookingRequest_DoctorUnavailable_ReturnsFailedValidationResult(
            int startTimeAdjustment, int endTimeAdjustment)
        {
            //arrange
            var existingBooking = InsertNewBooking();
            var request = GetBookingRequest();
            request.DoctorId = existingBooking.DoctorId;

            request.StartTime = existingBooking.StartTime.AddMinutes(startTimeAdjustment);
            request.EndTime = existingBooking.EndTime.AddMinutes(endTimeAdjustment);

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeFalse();
            res.Errors.Should().Contain("The requested appointment time with this doctor is not available");
        }

        [Test]
        public void ValidateRequestWithPatientId_AllChecksPass_ReturnsPassedValidationResult()
        {
            //arrange
            var existingPatient = InsertNewPatient();

            //act
            var res = _addBookingRequestValidator.ValidateRequest(existingPatient.Id);

            //assert
            res.PassedValidation.Should().BeTrue();
        }

        [Test]
        public void ValidateRequestWithPatientId_NoBooking_ReturnsFailedValidationResult()
        {
            //arrange
            var request = _fixture.Create<long>();

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeFalse();
            res.Errors.Should().Contain("A patient with that ID could not be found");
        }

        [Test]
        public void ValidateRequestWithBookingId_AllChecksPass_ReturnsPassedValidationResult()
        {
            //arrange
            var existingBooking = InsertNewBooking();

            //act
            var res = _addBookingRequestValidator.ValidateRequest(existingBooking.Id);

            //assert
            res.PassedValidation.Should().BeTrue();
        }

        [Test]
        public void ValidateRequestWithBookingId_NoBooking_ReturnsFailedValidationResult()
        {
            //arrange
            var request = _fixture.Create<Guid>();

            //act
            var res = _addBookingRequestValidator.ValidateRequest(request);

            //assert
            res.PassedValidation.Should().BeFalse();
            res.Errors.Should().Contain("A booking with that ID could not be found");
        }

        private Order InsertNewBooking()
        {
            var order = _fixture.Create<Order>();

            order.StartTime = _systemClock.Object.UtcNow.UtcDateTime.AddMinutes(1);
            order.EndTime = order.StartTime.AddMinutes(2);

            _context.Order.Add(order);
            _context.SaveChanges();
            return order;
        }

        private Patient InsertNewPatient()
        {
            var patient = _fixture.Create<Patient>();
            _context.Patient.Add(patient);
            _context.SaveChanges();
            return patient;
        }

        private Doctor InsertNewDoctor()
        {
            var doctor = _fixture.Create<Doctor>();
            doctor.Orders = new List<Order>();
            _context.Doctor.Add(doctor);
            _context.SaveChanges();
            return doctor;
        }

        private AddBookingRequest GetBookingRequest()
        {
            var bookingRequest = _fixture.Create<AddBookingRequest>();

            var patient = InsertNewPatient();
            var doctor = InsertNewDoctor();

            bookingRequest.PatientId = patient.Id;
            bookingRequest.DoctorId = doctor.Id;
            bookingRequest.StartTime = _systemClock.Object.UtcNow.UtcDateTime.AddMinutes(1);
            bookingRequest.EndTime = bookingRequest.StartTime.AddMinutes(2);

            return bookingRequest;
        }
    }
}