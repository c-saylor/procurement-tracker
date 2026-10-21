// Approve/Reject form enhancements: conditional rejection-reason field and a confirmation dialog.
// Every rule here is ALSO enforced on the server by ApprovalWorkflow; these checks only improve feedback.
$(function () {
    'use strict';

    const $form = $('#decision-form');
    if (!$form.length) {
        return; // the form is shown only to approvers on pending requests
    }

    const $notesGroup = $('#decision-notes-group');
    const $notes = $('#Decision_Notes');
    const $notesLabel = $('label[for="Decision_Notes"]');
    const $notesHelp = $('#decisionNotesHelp');
    const $confirmButton = $('#decision-confirm-button');
    const confirmModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('decision-confirm-modal'));
    let confirmed = false;

    function selectedAction() {
        return $form.find('input[name="Decision.Action"]:checked').val();
    }

    // Conditional display: hide the notes field until a decision is chosen, then relabel it.
    function updateNotesField() {
        const action = selectedAction();
        $notesGroup.toggleClass('d-none', !action);

        if (action === 'Rejected') {
            $notesLabel.text('Rejection reason (required)');
            $notesHelp.text('Explain why the request is being rejected. This reason is recorded in the audit trail.');
        } else {
            $notesLabel.text('Approval notes (optional)');
            $notesHelp.text('Optional context recorded with the approval.');
        }

        // If the field is already showing an error, re-check it against the new choice.
        if ($notes.hasClass('input-validation-error')) {
            $form.validate().element($notes);
        }
    }

    // Conditional validation rule: the reason is required only when Reject is selected.
    // (jQuery Unobtrusive has already created the validator from the C# attributes by this point.)
    $notes.rules('add', {
        required: {
            depends: function () { return selectedAction() === 'Rejected'; }
        },
        normalizer: function (value) { return $.trim(value); },
        messages: { required: 'Enter a reason for rejecting this request.' }
    });

    $form.on('change', 'input[name="Decision.Action"]', updateNotesField);

    // Ask for confirmation before submitting.
    $form.on('submit', function (event) {
        if (confirmed) {
            return; // already confirmed: let the browser submit
        }

        event.preventDefault();

        if (!$form.valid()) {
            return; // jQuery Validation displays the messages
        }

        const isReject = selectedAction() === 'Rejected';
        const summary = $form.data('request-summary');

        $('#decision-confirm-title').text(isReject ? 'Reject this request?' : 'Approve this request?');
        $('#decision-confirm-body').text(
            (isReject ? 'Reject ' : 'Approve ') + summary +
            '? The decision is recorded in the audit trail and cannot be undone.');
        $confirmButton
            .text(isReject ? 'Reject request' : 'Approve request')
            .toggleClass('btn-danger', isReject)
            .toggleClass('btn-success', !isReject);

        confirmModal.show();
    });

    $confirmButton.on('click', function () {
        confirmed = true;
        $confirmButton.prop('disabled', true).text('Saving…'); // prevents double submission
        $form[0].submit(); // native submit: skips the handlers above
    });

    // Initial state, including a redisplay after a server-side error (the chosen option is preserved).
    updateNotesField();
});