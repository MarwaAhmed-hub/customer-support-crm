using System.ComponentModel.DataAnnotations;
using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Communications.WebForms;

public sealed class WebFormSubmissionService(CrmDbContext db, ICustomersService customersService, ITicketsService ticketsService) : IWebFormSubmissionService
{
    // Same validator CustomersService uses for its own optional Email field.
    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<WebFormSubmissionResult> SubmitAsync(WebFormSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            // Honeypot: a real visitor never sees or fills this hidden field — silently drop.
            return WebFormSubmissionResult.HoneypotTriggered;
        }

        var email = request.Email.Trim();
        if (!EmailValidator.IsValid(email))
        {
            return WebFormSubmissionResult.InvalidEmail;
        }
        var normalizedEmail = EmailNormalizer.Normalize(email);
        var normalizedPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : PhoneNormalizer.Normalize(request.Phone);

        // Correction: "already a customer" means either identifier matches, not email alone — a
        // customer who first reached out by WhatsApp/SMS (phone on file, no email yet) submitting this
        // same phone number through the web form must land on that same record, not a duplicate.
        // Phone is checked first since it is the less ambiguous of the two once normalized.
        var customer = normalizedPhone is not null
            ? await customersService.GetByPhoneAsync(normalizedPhone, cancellationToken)
            : null;
        customer ??= await customersService.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (customer is null)
        {
            var (firstName, lastName) = SplitName(request.Name.Trim());
            var createResult = await customersService.CreateAsync(
                new CreateCustomerRequest(firstName, lastName, null, normalizedEmail, request.Phone?.Trim()), cancellationToken);
            // CreateAsync's own checks are already satisfied here (SplitName never produces an empty
            // name, and the email was just validated above), so Success is the only reachable outcome.
            customer = createResult.Customer!;
        }
        else
        {
            // Backfill only — never overwrite a contact detail the customer already has on file with a
            // different value from an anonymous submission. Only fills in what was previously blank.
            var needsEmail = string.IsNullOrWhiteSpace(customer.Email);
            var needsPhone = string.IsNullOrWhiteSpace(customer.Phone) && normalizedPhone is not null;
            if (needsEmail || needsPhone)
            {
                var updateResult = await customersService.UpdateAsync(customer.Id, new UpdateCustomerRequest(
                    customer.FirstName,
                    customer.LastName,
                    customer.CompanyName,
                    needsEmail ? normalizedEmail : customer.Email,
                    needsPhone ? normalizedPhone : customer.Phone), cancellationToken);
                if (updateResult.Outcome == CustomerOperationOutcome.Success)
                {
                    customer = updateResult.Customer;
                }
            }
        }

        var (categoryId, priorityId) = await ChannelTicketDefaults.ResolveAsync(db, cancellationToken);
        var systemUserId = await db.Users.Where(u => u.Email == DbSeeder.SystemUserEmail).Select(u => u.Id).SingleAsync(cancellationToken);

        var createTicketResult = await ticketsService.CreateAsync(
            new CreateTicketRequest(customer.Id, request.Subject.Trim(), request.Description.Trim(), categoryId, priorityId),
            systemUserId, sourceChannel: "WebForm", cancellationToken: cancellationToken);
        if (createTicketResult.Outcome != TicketOperationOutcome.Success)
        {
            throw new InvalidOperationException($"Failed to create ticket for web form submission: {createTicketResult.Outcome}");
        }

        var now = DateTime.UtcNow;
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = customer.Id,
            TicketId = createTicketResult.Ticket!.Id,
            OccurredAt = now,
            InteractionType = "web_form",
            Summary = request.Subject.Trim(),
            Details = request.Description.Trim(),
            UserId = null,
            FromAddress = normalizedEmail,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        return WebFormSubmissionResult.Success(createTicketResult.Ticket.Id, customer.Id);
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        if (fullName.Length == 0)
        {
            return ("Web", "Visitor");
        }

        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "(web form)");
    }
}
