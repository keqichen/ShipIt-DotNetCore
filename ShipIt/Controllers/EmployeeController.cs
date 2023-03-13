﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Repositories;

namespace ShipIt.Controllers
{

    [Route("employees")]
    public class EmployeeController : ControllerBase
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        private readonly IEmployeeRepository _employeeRepository;

        public EmployeeController(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        [HttpGet("")]
        //public EmployeeResponse Get([FromQuery] string name)
        public EmployeeResponse GetEM([FromQuery] int id)
        {
            //Log.Info($"Looking up employee by name: {name}");
            Log.Info($"Looking up employee by name: {id}");

            //var employee = new Employee(_employeeRepository.GetEmployeeByName(name));
            var employee = new Employee(_employeeRepository.GetEmployeeByName(id));

            Log.Info("Found employee: " + employee);
            return new EmployeeResponse(employee);
        }

        [HttpGet("{warehouseId}")]
        public EmployeeResponse Get([FromRoute] int warehouseId)
        {
            Log.Info(String.Format("Looking up employee by id: {0}", warehouseId));

            var employees = _employeeRepository
                .GetEmployeesByWarehouseId(warehouseId)
                .Select(e => new Employee(e));

            Log.Info(String.Format("Found employees: {0}", employees));
            
            return new EmployeeResponse(employees);
        }

        [HttpPost("")]
        public Response Post([FromBody] AddEmployeesRequest requestModel)
        {
            List<Employee> employeesToAdd = requestModel.Employees;

            if (employeesToAdd.Count == 0)
            {
                throw new MalformedRequestException("Expected at least one <employee> tag");
            }

            Log.Info("Adding employees: " + employeesToAdd);

            _employeeRepository.AddEmployees(employeesToAdd);

            Log.Debug("Employees added successfully");

            return new Response() { Success = true };
        }

        [HttpDelete("")]
        public void Delete([FromBody] RemoveEmployeeRequest requestModel)
        {
            // string name = requestModel.Name;
            // if (name == null)
            int id = requestModel.Id;
            if (id == 0)
            {
                throw new MalformedRequestException("Unable to parse name from request parameters");
            }

            try
            {
                //_employeeRepository.RemoveEmployee(name);
                _employeeRepository.RemoveEmployee(id);
            }
            catch (NoSuchEntityException)
            {
                //throw new NoSuchEntityException("No employee exists with name: " + name);
                throw new NoSuchEntityException("No employee exists with name: " + id);
            }
        }
    }
}
