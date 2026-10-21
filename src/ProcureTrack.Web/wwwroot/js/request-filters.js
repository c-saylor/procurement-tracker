// AJAX filtering for the purchase request list.
// Progressive enhancement: without JavaScript, the form still submits as a normal GET request.
$(function () {
    'use strict';

    const $form = $('#request-filter-form');
    if (!$form.length) {
        return;
    }

    const $container = $('#request-results-container');
    const $status = $('#results-status');
    const $from = $('#Filter_SubmittedFrom');
    const $to = $('#Filter_SubmittedTo');
    let debounceTimer = null;
    let pendingRequest = null;

    // Only non-empty fields, so URLs stay short and readable.
    function currentQuery() {
        return $form.find(':input')
            .filter(function () { return this.name && this.value; })
            .serialize();
    }

    // yyyy-MM-dd strings compare correctly as text.
    function dateRangeIsValid() {
        const from = $from.val();
        const to = $to.val();
        const valid = !from || !to || from <= to;
        $('#date-range-error').toggleClass('d-none', valid);
        $to.toggleClass('is-invalid', !valid).attr('aria-invalid', String(!valid));
        return valid;
    }

    function refreshResults() {
        if (!dateRangeIsValid()) {
            return;
        }

        const query = currentQuery();

        // Only the most recent request matters; cancel any earlier one still in flight.
        if (pendingRequest) {
            pendingRequest.abort();
        }

        $container.attr('aria-busy', 'true').addClass('is-loading');

        pendingRequest = $.ajax({
            url: $form.attr('action'),
            data: query,
            // cache:false adds a timestamp parameter so the browser never confuses this
            // HTML fragment with the full page at the same URL (e.g. after pressing Back).
            cache: false
        })
            .done(function (html) {
                $container.html(html);
                $status.text($container.find('#request-results').data('result-summary'));
                // Keep the address bar in sync so the filtered view can be bookmarked or shared.
                history.replaceState(null, '', $form.attr('action') + (query ? '?' + query : ''));
            })
            .fail(function (xhr, textStatus) {
                if (textStatus === 'abort') {
                    return;
                }
                $container.html(
                    '<div class="alert alert-danger" role="alert">' +
                    'Results could not be loaded. Check your connection and try again.</div>');
            })
            .always(function () {
                $container.attr('aria-busy', 'false').removeClass('is-loading');
            });
    }

    // Dropdowns and dates: update immediately.
    $form.on('change', 'select, input[type="date"]', refreshResults);

    // Search box: wait for a 300 ms pause in typing instead of querying on every keystroke.
    $form.on('input', 'input[type="search"]', function () {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(refreshResults, 300);
    });

    // The Apply button and the Enter key use AJAX too when JavaScript is available.
    $form.on('submit', function (event) {
        event.preventDefault();
        refreshResults();
    });
});