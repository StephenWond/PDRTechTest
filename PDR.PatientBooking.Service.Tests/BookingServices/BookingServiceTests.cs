using System;
using System.Linq;
using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Internal;
using Moq;
using NUnit.Framework;
using PDR.PatientBooking.Data;
using PDR.PatientBooking.Data.Models;
using PDR.PatientBooking.Service.BookingServices;
using PDR.PatientBooking.Service.BookingServices.Requests;
using PDR.PatientBooking.Service.BookingServices.Responses;
using PDR.PatientBooking.Service.BookingServices.Validation;
using PDR.PatientBooking.Service.Validation;

namespace PDR.PatientBooking.Service.Tests.BookingServices
{
    [TestFixture]
    public class BookingServiceTests
    {
        private MockRepository _mockRepository;
        private IFixture _fixture;

        private PatientBookingContext _context;
        private Mock<IBookingRequestValidator> _validator;
        private Mock<ISystemClock> _systemClock;

        private BookingService _bookingService;

        [SetUp]
        public void SetUp()
        {
            // Boilerplate
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _fixture = new Fixture();

            //Prevent fixture from generating circular references
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior(1));

            // Mock setup
            _context = new PatientBookingContext(new DbContextOptionsBuilder<PatientBookingContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            _validator = _mockRepository.Create<IBookingRequestValidator>();
            _systemClock = new Mock<ISystemClock>();

            // Mock default
            SetupMockDefaults();

            // Sut instantiation
            _bookingService = new BookingService(
                _context,
                _validator.Object,
                _systemClock.Object
            );
        }

        private void SetupMockDefaults()
        {
            _validator.Setup(x => x.ValidateRequest(It.IsAny<AddBookingRequest>()))
                .Returns(new PdrValidationResult(true));

            _validator.Setup(x => x.ValidateRequest(It.IsAny<long>()))
                .Returns(new PdrValidationResult(true));

            _validator.Setup(x => x.ValidateRequest(It.IsAny<Guid>()))
                .Returns(new PdrValidationResult(true));

            _systemClock
                .Setup(x => x.UtcNow)
                .Returns(DateTime.UtcNow);
        }

        [Test]
        public void AddBooking_ValidatesRequest()
        {
            //arrange
            var patient = _fixture.Create<Patient>();
            _context.Patient.Add(patient);
            _context.SaveChanges();

            var request = _fixture.Create<AddBookingRequest>();
            request.PatientId = patient.Id;


            //act
            _bookingService.AddBooking(request);

            //assert
            _validator.Verify(x => x.ValidateRequest(request), Times.Once);
        }

        [Test]
        public void AddBooking_ValidatorFails_ThrowsArgumentException()
        {
            //arrange
            var failedValidationResult = new PdrValidationResult(false, _fixture.Create<string>());
            _validator.Setup(x => x.ValidateRequest(It.IsAny<AddBookingRequest>())).Returns(failedValidationResult);

            //act
            var exception =
                Assert.Throws<ArgumentException>(() =>
                    _bookingService.AddBooking(_fixture.Create<AddBookingRequest>()));

            //assert
            exception.Message.Should().Be(failedValidationResult.Errors.First());
        }

        [Test]
        public void AddBooking_AddsBookingToContextWithGeneratedId()
        {
            //arrange
            var patient = _fixture.Create<Patient>();
            _context.Patient.Add(patient);
            _context.SaveChanges();

            var request = _fixture.Create<AddBookingRequest>();
            request.PatientId = patient.Id;

            var expected = new Order
            {
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                SurgeryType = 0,
                PatientId = request.PatientId,
                DoctorId = request.DoctorId,
                Patient = patient,
                Doctor = null
            };

            //act
            _bookingService.AddBooking(request);

            //assert
            _context.Order.Should().ContainEquivalentOf(expected, options => options.Excluding(order => order.Id));
        }

        [Test]
        public void GetNextBookingByPatientId_ValidatorFails_ThrowsArgumentException()
        {
            //arrange
            var failedValidationResult = new PdrValidationResult(false, _fixture.Create<string>());
            _validator.Setup(x => x.ValidateRequest(It.IsAny<long>())).Returns(failedValidationResult);

            //act
            var exception =
                Assert.Throws<ArgumentException>(() =>
                    _bookingService.GetNextBooking(_fixture.Create<long>()));

            //assert
            exception.Message.Should().Be(failedValidationResult.Errors.First());
        }

        [Test]
        public void GetNextBookingByPatientId_NoBooking_ReturnsEmptyResponse()
        {
            //arrange
            var id = _fixture.Create<long>();

            //act
            var res = _bookingService.GetNextBooking(id);

            //assert
            res.Should().BeNull();
        }

        [Test]
        public void GetNextBookingByPatientId_ReturnsNextBooking()
        {
            //arrange
            var request = _fixture.Create<Order>();
            request.StartTime = DateTime.UtcNow.AddMinutes(1);
            request.EndTime = DateTime.UtcNow.AddMinutes(2);
            _context.Order.Add(request);
            _context.SaveChanges();

            var expected = new GetBookingResponse
            {
                Id = request.Id,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                PatientId = request.PatientId,
                DoctorId = request.DoctorId,
                SurgeryType = (SurgeryType) request.SurgeryType
            };

            //act
            var res = _bookingService.GetNextBooking(request.PatientId);

            //assert
            res.Should().BeEquivalentTo(expected);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
        }
    }
}