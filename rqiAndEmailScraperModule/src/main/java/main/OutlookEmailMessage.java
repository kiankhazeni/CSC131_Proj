package main;

public class OutlookEmailMessage {
    private final String id;
    private final String internetMessageId;
    private final String from;
    private final String subject;
    private final String received;
    private final String body;

    public OutlookEmailMessage(
            String id,
            String internetMessageId,
            String from,
            String subject,
            String received,
            String body
    ) {
        this.id = id;
        this.internetMessageId = internetMessageId;
        this.from = from;
        this.subject = subject;
        this.received = received;
        this.body = body;
    }

    public String getStableId() {
        if (internetMessageId != null && !internetMessageId.isBlank()) {
            return internetMessageId;
        }
        return id;
    }

    public String getFrom() {
        return from;
    }

    public String getSubject() {
        return subject;
    }

    public String getReceived() {
        return received;
    }

    public String getBody() {
        return body;
    }
}