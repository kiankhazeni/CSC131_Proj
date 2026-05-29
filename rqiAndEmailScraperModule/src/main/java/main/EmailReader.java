package main;

import com.microsoft.graph.models.Message;
import com.microsoft.graph.models.MessageCollectionResponse;
import com.microsoft.graph.serviceclient.GraphServiceClient;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;

public class EmailReader {

    private final GraphServiceClient graphClient;

    public EmailReader(GraphServiceClient graphClient) {
        this.graphClient = graphClient;
    }

    public List<OutlookEmailMessage> fetchRecentMessages(int maxMessages, int pastDays) throws Exception {
        OffsetDateTime cutoff = OffsetDateTime.now(ZoneOffset.UTC).minusDays(pastDays);

        MessageCollectionResponse response = graphClient
                .me()
                .mailFolders()
                .byMailFolderId("inbox")
                .messages()
                .get(requestConfig -> {
                    requestConfig.headers.add("Prefer", "IdType=\"ImmutableId\", outlook.body-content-type=\"text\"");

                    requestConfig.queryParameters.select = new String[] {
                            "id",
                            "internetMessageId",
                            "from",
                            "subject",
                            "receivedDateTime",
                            "body"
                    };

                    requestConfig.queryParameters.top = maxMessages;
                    requestConfig.queryParameters.orderby = new String[] {
                            "receivedDateTime DESC"
                    };

                    requestConfig.queryParameters.filter =
                            "receivedDateTime ge " + cutoff.toString();
                });

        List<OutlookEmailMessage> result = new ArrayList<>();

        if (response == null || response.getValue() == null) {
            return result;
        }

        for (Message message : response.getValue()) {
            String id = message.getId();
            String internetMessageId = message.getInternetMessageId();

            String subject = message.getSubject() != null
                    ? message.getSubject()
                    : "[No Subject]";

            String from = "[Unknown]";
            if (message.getFrom() != null
                    && message.getFrom().getEmailAddress() != null) {
                String name = message.getFrom().getEmailAddress().getName();
                String address = message.getFrom().getEmailAddress().getAddress();

                if (name != null && !name.isBlank() && address != null && !address.isBlank()) {
                    from = name + " <" + address + ">";
                } else if (address != null && !address.isBlank()) {
                    from = address;
                } else if (name != null && !name.isBlank()) {
                    from = name;
                }
            }

            String received = message.getReceivedDateTime() != null
                    ? message.getReceivedDateTime().toString()
                    : "[No Date]";

            String body = "";
            if (message.getBody() != null && message.getBody().getContent() != null) {
                body = message.getBody().getContent();
            }

            result.add(new OutlookEmailMessage(
                    id,
                    internetMessageId,
                    from,
                    subject,
                    received,
                    body
            ));
        }

        return result;
    }
}